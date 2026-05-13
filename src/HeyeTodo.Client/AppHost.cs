using System;
using HeyeTodo.Client.Persistence;
using HeyeTodo.Client.ViewModels;
using HeyeTodo.Client.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client;

public static class AppHost
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static void BootstrapCore()
    {
        var services = new ServiceCollection();

        services.AddSingleton<Views.MainWindow>();
        services.AddSingleton<IPersistenceStore, FilePersistenceStore>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<TestPageViewModel>();
        services.AddSingleton<TaskPanelViewModel>();
        services.AddSingleton<GanttChartViewModel>();
        services.AddTransient<TestPageView>();
        services.AddTransient<TaskPanelView>();
        services.AddTransient<GanttChartView>();

        Services = services.BuildServiceProvider();
    }
}
