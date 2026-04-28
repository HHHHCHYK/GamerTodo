using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Auth;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

/// <summary>Coming-soon placeholder (M7).</summary>
public sealed class MiniGamesHubViewModel : ViewModelBase
{
}

/// <summary>Settings view model (language, server URL, roles, planning mode). Wired up in M1.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ISettingsService _settings;
    private readonly ClientSession _session;
    private readonly INavigationService _navigation;
    private readonly IClientLogger _logger;

    public ObservableCollection<LanguageOption> Languages { get; } = new()
    {
        new LanguageOption("auto", "Settings.Language.Auto"),
        new LanguageOption("en", "Settings.Language.English"),
        new LanguageOption("zh", "Settings.Language.Chinese"),
    };

    public ObservableCollection<PlanningModeOption> PlanningModes { get; } = new()
    {
        new PlanningModeOption("rule", "Settings.PlanningMode.Rule"),
        new PlanningModeOption("server", "Settings.PlanningMode.Server"),
        new PlanningModeOption("client", "Settings.PlanningMode.Client"),
    };

    public ObservableCollection<LogLevelOption> LogLevels { get; } = new()
    {
        new LogLevelOption("Information", "Information"),
        new LogLevelOption("Warning", "Warning"),
        new LogLevelOption("Error", "Error"),
    };

    public ObservableCollection<RoleOption> RoleOptions { get; } = new()
    {
        new RoleOption(RoleType.Producer, "Roles.Producer"),
        new RoleOption(RoleType.Designer, "Roles.Designer"),
        new RoleOption(RoleType.Artist, "Roles.Artist"),
        new RoleOption(RoleType.Programmer, "Roles.Programmer"),
        new RoleOption(RoleType.SoundDesigner, "Roles.SoundDesigner"),
    };

    [ObservableProperty] private string _serverBaseUrl = string.Empty;
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private PlanningModeOption? _selectedPlanningMode;
    [ObservableProperty] private LogLevelOption? _selectedLogLevel;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public SettingsViewModel(ApiClient api, ISettingsService settings, ClientSession session, INavigationService navigation, IClientLogger logger)
    {
        _api = api;
        _settings = settings;
        _session = session;
        _navigation = navigation;
        _logger = logger;

        ServerBaseUrl = settings.Current.ServerBaseUrl;
        SelectedLanguage = FindLanguage(settings.Current.Language);
        SelectedPlanningMode = FindPlanningMode(settings.Current.PlanningMode);
        SelectedLogLevel = FindLogLevel(settings.Current.LogLevel);

        var roles = (RoleType)settings.Current.Roles;
        foreach (var option in RoleOptions)
        {
            option.IsSelected = roles.HasFlag(option.Value);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var serverBaseUrl = NormalizeServerBaseUrl(ServerBaseUrl);
            var selectedLanguage = SelectedLanguage?.Value ?? "auto";
            var selectedPlanningMode = SelectedPlanningMode?.Value ?? "rule";
            var selectedLogLevel = SelectedLogLevel?.Value ?? "Information";

            RoleType roles = RoleType.None;
            foreach (var option in RoleOptions)
            {
                if (option.IsSelected) roles |= option.Value;
            }

            var localChanged = !string.Equals(_settings.Current.ServerBaseUrl, serverBaseUrl, StringComparison.Ordinal)
                || !string.Equals(_settings.Current.Language, selectedLanguage, StringComparison.Ordinal)
                || !string.Equals(_settings.Current.PlanningMode, selectedPlanningMode, StringComparison.Ordinal);

            var logLevelChanged = !string.Equals(_settings.Current.LogLevel, selectedLogLevel, StringComparison.OrdinalIgnoreCase);

            var rolesChanged = _settings.Current.Roles != (int)roles;

            if (localChanged)
            {
                _settings.UpdateLocal(serverBaseUrl, selectedLanguage, selectedPlanningMode);
            }

            if (logLevelChanged)
            {
                _settings.UpdateLogLevel(selectedLogLevel);
            }

            if (rolesChanged)
            {
                var updatedUser = await _api.UpdateRolesAsync(new UpdateRolesRequest(roles, null));
                if (updatedUser is null)
                {
                    ErrorMessage = localChanged
                        ? LocalizationService.Instance["Settings.SavePartialFailed"]
                        : LocalizationService.Instance["Settings.SaveFailed"];
                    await _logger.LogOperationAsync("Settings", "Save", ClientLogLevel.Warning, "Settings role update failed.", new Dictionary<string, object?>
                    {
                        ["localChanged"] = localChanged,
                        ["roles"] = roles,
                    });
                    return;
                }

                _settings.UpdateRoles(updatedUser.Roles, updatedUser.ActiveRoleContext);
                _session.Roles = updatedUser.Roles;
                _session.ActiveRoleContext = updatedUser.ActiveRoleContext;
            }

            if (localChanged || logLevelChanged || rolesChanged)
            {
                StatusMessage = LocalizationService.Instance["Settings.SaveSuccess"];
                await _logger.LogOperationAsync("Settings", "Save", ClientLogLevel.Information, "Settings saved.", new Dictionary<string, object?>
                {
                    ["localChanged"] = localChanged,
                    ["language"] = selectedLanguage,
                    ["planningMode"] = selectedPlanningMode,
                    ["logLevelChanged"] = logLevelChanged,
                    ["logLevel"] = selectedLogLevel,
                    ["rolesChanged"] = rolesChanged,
                    ["roles"] = roles,
                });
            }
        }
        catch (UriFormatException ex)
        {
            ErrorMessage = LocalizationService.Instance["Settings.ServerUrlInvalid"];
            await _logger.LogUserOperationExceptionAsync("SettingsSave", ex);
        }
        catch (Exception ex)
        {
            ErrorMessage = LocalizationService.Instance["Settings.SaveFailed"];
            await _logger.LogUserOperationExceptionAsync("SettingsSave", ex, new Dictionary<string, object?>
            {
                ["selectedLanguage"] = SelectedLanguage?.Value,
                ["selectedPlanningMode"] = SelectedPlanningMode?.Value,
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _api.LogoutAsync();
        _session.Reset();
        await _logger.LogOperationAsync("Settings", "Logout", ClientLogLevel.Information, "Logout completed from settings.");
        _navigation.NavigateTo<LoginViewModel>();
    }

    private static string NormalizeServerBaseUrl(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        _ = new Uri(trimmed, UriKind.Absolute);
        return trimmed;
    }

    private LanguageOption FindLanguage(string value)
        => FindOption(Languages, value) ?? Languages[0];

    private PlanningModeOption FindPlanningMode(string value)
        => FindOption(PlanningModes, value) ?? PlanningModes[0];

    private LogLevelOption FindLogLevel(string value)
        => FindOption(LogLevels, value) ?? LogLevels[0];

    private static TOption? FindOption<TOption>(ObservableCollection<TOption> options, string value)
        where TOption : OptionItem
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }
}

public abstract class OptionItem : ObservableObject
{
    public string Value { get; }
    public string LabelKey { get; }
    public string Label => LocalizationService.Instance[LabelKey];

    protected OptionItem(string value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
    }
}

public sealed class LanguageOption(string value, string labelKey) : OptionItem(value, labelKey)
{
}

public sealed class PlanningModeOption(string value, string labelKey) : OptionItem(value, labelKey)
{
}

public sealed class LogLevelOption(string value, string labelKey) : OptionItem(value, labelKey)
{
}

public sealed partial class SplashViewModel : ViewModelBase
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _status = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string? _errorMessage;
}
