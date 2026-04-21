using System;
using HeyeTodo.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client.Infrastructure.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public event Action<ViewModelBase>? Navigated;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var viewModel = _services.GetRequiredService<TViewModel>();
        Navigate(viewModel);
    }

    public void Navigate(ViewModelBase viewModel)
    {
        Navigated?.Invoke(viewModel);
    }
}
