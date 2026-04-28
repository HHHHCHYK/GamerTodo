using System;
using System.Collections.Generic;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client.Infrastructure.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly IClientLogger _logger;

    public NavigationService(IServiceProvider services, IClientLogger logger)
    {
        _services = services;
        _logger = logger;
    }

    public event Action<ViewModelBase>? Navigated;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var viewModel = _services.GetRequiredService<TViewModel>();
        Navigate(viewModel);
    }

    public void Navigate(ViewModelBase viewModel)
    {
        _ = _logger.LogOperationAsync("Navigation", "Navigate", ClientLogLevel.Information, "Navigation target changed.", new Dictionary<string, object?>
        {
            ["viewModel"] = viewModel.GetType().Name,
        });
        Navigated?.Invoke(viewModel);
    }
}
