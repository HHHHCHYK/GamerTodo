using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _current = null!;

    public MainWindowViewModel(INavigationService navigationService, ApiClient api, ClientSession session)
    {
        navigationService.Navigated += viewModel => Current = viewModel;

        _ = InitializeAsync(navigationService, api, session);
    }

    private async Task InitializeAsync(INavigationService navigationService, ApiClient api, ClientSession session)
    {
        if (!api.IsAuthenticated)
        {
            session.Reset();
            navigationService.NavigateTo<LoginViewModel>();
            return;
        }

        var user = await api.GetCurrentUserAsync();
        if (user is null)
        {
            await api.LogoutAsync();
            session.Reset();
            navigationService.NavigateTo<LoginViewModel>();
            return;
        }

        session.IsAuthenticated = true;
        session.UserId = user.Id;
        session.DisplayName = user.DisplayName;
        session.Roles = user.Roles;
        session.ActiveRoleContext = user.ActiveRoleContext;

        if (user.Roles == RoleType.None)
        {
            navigationService.NavigateTo<RoleSelectionViewModel>();
            return;
        }

        navigationService.NavigateTo<ShellViewModel>();
    }
}
