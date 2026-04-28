using System.Collections.Generic;
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

public sealed partial class RoleSelectionViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ISettingsService _settings;
    private readonly ClientSession _session;
    private readonly INavigationService _navigation;
    private readonly IClientLogger _logger;

    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<RoleOption> Options { get; } = new()
    {
        new RoleOption(RoleType.Producer,      "Roles.Producer"),
        new RoleOption(RoleType.Designer,      "Roles.Designer"),
        new RoleOption(RoleType.Artist,        "Roles.Artist"),
        new RoleOption(RoleType.Programmer,    "Roles.Programmer"),
        new RoleOption(RoleType.SoundDesigner, "Roles.SoundDesigner"),
    };

    public RoleSelectionViewModel(ApiClient api, ISettingsService settings, ClientSession session, INavigationService navigation, IClientLogger logger)
    {
        _api = api;
        _settings = settings;
        _session = session;
        _navigation = navigation;
        _logger = logger;
        // Pre-check options that are already saved.
        foreach (var o in Options)
        {
            o.IsSelected = ((RoleType)_settings.Current.Roles).HasFlag(o.Value);
        }
    }

    [RelayCommand]
    private async Task Continue()
    {
        ErrorMessage = null;
        IsBusy = true;

        RoleType combined = RoleType.None;
        foreach (var o in Options)
            if (o.IsSelected) combined |= o.Value;

        try
        {
            var updated = await _api.UpdateRolesAsync(new UpdateRolesRequest(combined, null));
            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["RoleSelection.SaveFailed"];
                await _logger.LogOperationAsync("RoleSelection", "Continue", ClientLogLevel.Warning, "Role selection save failed.", new Dictionary<string, object?>
                {
                    ["roles"] = combined,
                });
                return;
            }

            _settings.UpdateRoles(updated.Roles, updated.ActiveRoleContext);

            _session.Roles = updated.Roles;
            _session.ActiveRoleContext = updated.ActiveRoleContext;
            await _logger.LogOperationAsync("RoleSelection", "Continue", ClientLogLevel.Information, "Role selection saved.", new Dictionary<string, object?>
            {
                ["roles"] = updated.Roles,
                ["activeRoleContext"] = updated.ActiveRoleContext,
            });
            _navigation.NavigateTo<ShellViewModel>();
        }
        catch (System.Exception ex)
        {
            ErrorMessage = LocalizationService.Instance["RoleSelection.SaveFailed"];
            await _logger.LogUserOperationExceptionAsync("RoleSelectionContinue", ex, new Dictionary<string, object?>
            {
                ["roles"] = combined,
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Skip()
    {
        await _logger.LogOperationAsync("RoleSelection", "Skip", ClientLogLevel.Information, "Role selection skipped.");
        _navigation.NavigateTo<ShellViewModel>();
    }
}

public sealed partial class RoleOption : ObservableObject
{
    public RoleType Value { get; }
    public string LabelKey { get; }
    public string Label => LocalizationService.Instance[LabelKey];

    [ObservableProperty] private bool _isSelected;

    public RoleOption(RoleType value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
    }
}
