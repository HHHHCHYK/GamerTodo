using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client;

public partial class App : Avalonia.Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppHost.BootstrapCore();
        _services = AppHost.Services;
        var logger = _services.GetRequiredService<IClientLogger>();
        RegisterUnhandledExceptionLogging(logger);

        try
        {
            _ = logger.LogOperationAsync("App", "Startup", ClientLogLevel.Information, "Application services built.");
            var navigation = _services.GetRequiredService<INavigationService>();
            var mainWindowViewModel = _services.GetRequiredService<MainWindowViewModel>();
            navigation.Navigated += vm => mainWindowViewModel.Current = vm;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = _services.GetRequiredService<Views.MainWindow>();
                desktop.MainWindow.DataContext = mainWindowViewModel;
                desktop.Exit += (_, e) =>
                {
                    logger.LogOperationAsync("App", "Exit", ClientLogLevel.Information, "Application exit requested.", new System.Collections.Generic.Dictionary<string, object?>
                    {
                        ["applicationExitCode"] = e.ApplicationExitCode,
                    }).GetAwaiter().GetResult();
                };
            }

            _ = logger.LogOperationAsync("App", "Startup", ClientLogLevel.Information, "Application startup completed.");
        }
        catch (Exception ex)
        {
            logger.LogOperationAsync("App", "Startup", ClientLogLevel.Error, "Application startup failed.", exception: ex).GetAwaiter().GetResult();
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterUnhandledExceptionLogging(IClientLogger logger)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            logger.LogOperationAsync("App", "UnhandledException", ClientLogLevel.Error, "Unhandled domain exception captured.", new System.Collections.Generic.Dictionary<string, object?>
            {
                ["isTerminating"] = e.IsTerminating,
            }, exception).GetAwaiter().GetResult();
        };

        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (e.Exception is InvalidOperationException or NullReferenceException)
            {
                _ = logger.LogOperationAsync("App", "FirstChanceException", ClientLogLevel.Warning, "Potentially important first chance exception captured.", exception: e.Exception);
            }
        };
    }
}
