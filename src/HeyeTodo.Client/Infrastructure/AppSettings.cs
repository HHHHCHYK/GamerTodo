using System;
using System.IO;
using System.Text.Json;

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
