using System;
using System.Net.Http;
using HeyeTodo.Client.Persistence;
using HeyeTodo.Client.Services;
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
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<IClientSessionStore, ClientSessionStore>();
        services.AddSingleton(new HttpClient());
        services.AddSingleton<HeyeTodoApiClient>();
        services.AddSingleton<SyncCoordinator>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<AccountViewModel>();
        services.AddSingleton<TestPageViewModel>();
        services.AddSingleton<TaskPanelViewModel>();
        services.AddTransient<AccountView>();
        services.AddTransient<TestPageView>();
        services.AddTransient<TaskPanelView>();

        Services = services.BuildServiceProvider();
    }
}
