using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ISyncCoordinator _sync;
    private readonly ClientSession _session;
    private readonly INavigationService _navigation;
    private readonly IClientLogger _logger;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public LoginViewModel(ApiClient api, ISyncCoordinator sync, ClientSession session, INavigationService navigation, IClientLogger logger)
    {
        _api = api;
        _sync = sync;
        _session = session;
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var r = await _api.LoginAsync(Email.Trim(), Password);
            if (r is null)
            {
                ErrorMessage = "Login failed";
                await _logger.LogOperationAsync("Auth", "Login", ClientLogLevel.Warning, "Login failed.", new Dictionary<string, object?>
                {
                    ["email"] = Email.Trim(),
                });
                return;
            }
            _session.IsAuthenticated = true;
            _session.UserId = r.User.Id;
            _session.DisplayName = r.User.DisplayName;
            _session.Roles = r.User.Roles;
            _session.ActiveRoleContext = r.User.ActiveRoleContext;
            await _sync.StartAsync(r.User.Id);
            await _logger.LogOperationAsync("Auth", "Login", ClientLogLevel.Information, "Login completed.", new Dictionary<string, object?>
            {
                ["userId"] = r.User.Id,
                ["roles"] = r.User.Roles,
            });

            if (r.User.Roles == RoleType.None)
            {
                _navigation.NavigateTo<RoleSelectionViewModel>();
                return;
            }

            _navigation.NavigateTo<ShellViewModel>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = "Cannot connect to server";
            await _logger.LogUserOperationExceptionAsync("Login", ex, new Dictionary<string, object?>
            {
                ["email"] = Email.Trim(),
            });
        }
        finally
        {
            Password = string.Empty;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoToRegister()
    {
        _navigation.NavigateTo<RegisterViewModel>();
    }
}
