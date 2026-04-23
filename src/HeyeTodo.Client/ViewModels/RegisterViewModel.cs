using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Auth;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class RegisterViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ClientSession _session;
    private readonly INavigationService _navigation;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public RegisterViewModel(ApiClient api, ClientSession session, INavigationService navigation)
    {
        _api = api;
        _session = session;
        _navigation = navigation;
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
            if (r is null) { ErrorMessage = "Registration failed"; return; }
            _session.IsAuthenticated = true;
            _session.UserId = r.User.Id;
            _session.DisplayName = r.User.DisplayName;
            _session.Roles = r.User.Roles;
            _session.ActiveRoleContext = r.User.ActiveRoleContext;

            // New user → go to role selection (skippable).
            _navigation.NavigateTo<RoleSelectionViewModel>();
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
