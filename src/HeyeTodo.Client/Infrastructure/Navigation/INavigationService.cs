using System;
using HeyeTodo.Client.ViewModels;

namespace HeyeTodo.Client.Infrastructure.Navigation;

public interface INavigationService
{
    event Action<ViewModelBase>? Navigated;

    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void Navigate(ViewModelBase viewModel);
}
