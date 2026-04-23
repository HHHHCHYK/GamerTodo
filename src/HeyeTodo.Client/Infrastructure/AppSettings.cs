using System;
using System.IO;
using System.Text.Json;
using System.Globalization;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.Infrastructure;

public sealed class AppSettings
{
    /// <summary>Server base URL, e.g. https://heyetodo.example.com .</summary>
    public string ServerBaseUrl { get; set; } = "http://localhost:5254";

    /// <summary><c>auto</c> | <c>en</c> | <c>zh</c></summary>
    public string Language { get; set; } = "auto";

    /// <summary>Planning service mode.  server | client | rule</summary>
    public string PlanningMode { get; set; } = "rule";

    /// <summary>OpenAI / Claude API key if user chose local-key planning.</summary>
    public string? LocalLlmApiKey { get; set; }

    /// <summary>Roles chosen by the user (bitmask from <c>RoleType</c>).</summary>
    public int Roles { get; set; }

    /// <summary>Active role context (nullable int = RoleType value or null).</summary>
    public int? ActiveRoleContext { get; set; }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                var json = File.ReadAllText(AppPaths.SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* fall through */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(AppPaths.SettingsPath, json);
    }
}

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler<SettingsChangedEventArgs>? Changed;

    void UpdateLocal(string? serverBaseUrl = null, string? language = null, string? planningMode = null);
    void UpdateRoles(RoleType roles, RoleType? activeRoleContext);
}

public sealed class SettingsChangedEventArgs : EventArgs
{
    public bool ServerBaseUrlChanged { get; init; }
    public bool LanguageChanged { get; init; }
    public bool PlanningModeChanged { get; init; }
    public bool RolesChanged { get; init; }
}

public sealed class SettingsService : ISettingsService
{
    private readonly object _gate = new();

    public SettingsService(AppSettings initial)
    {
        Current = initial;
        ApplyLanguage(initial.Language);
    }

    public AppSettings Current { get; }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void UpdateLocal(string? serverBaseUrl = null, string? language = null, string? planningMode = null)
    {
        SettingsChangedEventArgs? args = null;

        lock (_gate)
        {
            var serverChanged = serverBaseUrl is not null && !string.Equals(Current.ServerBaseUrl, serverBaseUrl, StringComparison.Ordinal);
            var languageChanged = language is not null && !string.Equals(Current.Language, language, StringComparison.Ordinal);
            var planningChanged = planningMode is not null && !string.Equals(Current.PlanningMode, planningMode, StringComparison.Ordinal);

            if (!serverChanged && !languageChanged && !planningChanged)
            {
                return;
            }

            if (serverChanged)
            {
                Current.ServerBaseUrl = serverBaseUrl!;
            }

            if (languageChanged)
            {
                Current.Language = language!;
            }

            if (planningChanged)
            {
                Current.PlanningMode = planningMode!;
            }

            SettingsStore.Save(Current);

            if (languageChanged)
            {
                ApplyLanguage(Current.Language);
            }

            args = new SettingsChangedEventArgs
            {
                ServerBaseUrlChanged = serverChanged,
                LanguageChanged = languageChanged,
                PlanningModeChanged = planningChanged,
            };
        }

        Changed?.Invoke(this, args);
    }

    public void UpdateRoles(RoleType roles, RoleType? activeRoleContext)
    {
        SettingsChangedEventArgs? args = null;

        lock (_gate)
        {
            var rolesChanged = Current.Roles != (int)roles || Current.ActiveRoleContext != (activeRoleContext is null ? null : (int)activeRoleContext.Value);
            if (!rolesChanged)
            {
                return;
            }

            Current.Roles = (int)roles;
            Current.ActiveRoleContext = activeRoleContext is null ? null : (int)activeRoleContext.Value;
            SettingsStore.Save(Current);

            args = new SettingsChangedEventArgs
            {
                RolesChanged = true,
            };
        }

        Changed?.Invoke(this, args);
    }

    private static void ApplyLanguage(string language)
    {
        var culture = language switch
        {
            "zh" => new CultureInfo("zh"),
            "en" => new CultureInfo("en"),
            _ => LocalizationService.DetectSystemCulture(),
        };

        LocalizationService.Instance.Culture = culture;
    }
}
