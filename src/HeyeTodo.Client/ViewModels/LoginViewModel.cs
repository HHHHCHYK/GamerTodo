using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ClientSession _session;
    private readonly INavigationService _navigation;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public LoginViewModel(ApiClient api, ClientSession session, INavigationService navigation)
    {
        _api = api;
        _session = session;
        _navigation = navigation;
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
                return;
            }
            _session.IsAuthenticated = true;
            _session.UserId = r.User.Id;
            _session.DisplayName = r.User.DisplayName;
            _session.Roles = r.User.Roles;
            _session.ActiveRoleContext = r.User.ActiveRoleContext;

            if (r.User.Roles == RoleType.None)
            {
                _navigation.NavigateTo<RoleSelectionViewModel>();
                return;
            }

            _navigation.NavigateTo<ShellViewModel>();
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
