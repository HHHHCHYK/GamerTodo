using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Auth;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class RegisterViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ISyncCoordinator _sync;
    private readonly ClientSession _session;
    private readonly INavigationService _navigation;
    private readonly IClientLogger _logger;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public RegisterViewModel(ApiClient api, ISyncCoordinator sync, ClientSession session, INavigationService navigation, IClientLogger logger)
    {
        _api = api;
        _sync = sync;
        _session = session;
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var req = new RegisterRequest(Email.Trim(), Password, DisplayName.Trim(), RoleType.None);
            var r = await _api.RegisterAsync(req);
            if (r is null)
            {
                ErrorMessage = "Registration failed";
                await _logger.LogOperationAsync("Auth", "Register", ClientLogLevel.Warning, "Registration failed.", new Dictionary<string, object?>
                {
                    ["email"] = Email.Trim(),
                    ["displayName"] = DisplayName.Trim(),
                });
                return;
            }
            _session.IsAuthenticated = true;
            _session.UserId = r.User.Id;
            _session.DisplayName = r.User.DisplayName;
            _session.Roles = r.User.Roles;
            _session.ActiveRoleContext = r.User.ActiveRoleContext;
            await _sync.StartAsync(r.User.Id);
            await _logger.LogOperationAsync("Auth", "Register", ClientLogLevel.Information, "Registration completed.", new Dictionary<string, object?>
            {
                ["userId"] = r.User.Id,
                ["roles"] = r.User.Roles,
            });

            // New user → go to role selection (skippable).
            _navigation.NavigateTo<RoleSelectionViewModel>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = "Cannot connect to server";
            await _logger.LogUserOperationExceptionAsync("Register", ex, new Dictionary<string, object?>
            {
                ["email"] = Email.Trim(),
                ["displayName"] = DisplayName.Trim(),
            });
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToLogin()
    {
        _navigation.NavigateTo<LoginViewModel>();
    }
}
