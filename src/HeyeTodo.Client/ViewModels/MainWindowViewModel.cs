using CommunityToolkit.Mvvm.ComponentModel;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _current = null!;

    public MainWindowViewModel(INavigationService navigationService, ApiClient api)
    {
        navigationService.Navigated += viewModel => Current = viewModel;

        if (api.IsAuthenticated)
        {
            navigationService.NavigateTo<ShellViewModel>();
        }
        else
        {
            navigationService.NavigateTo<LoginViewModel>();
        }
    }
}
