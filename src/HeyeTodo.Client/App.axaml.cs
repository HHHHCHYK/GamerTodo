using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HeyeTodo.Client.ViewModels;
using HeyeTodo.Client.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        AppHost.BootstrapCore();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow
            {
                DataContext = AppHost.Services.GetRequiredService<SplashViewModel>(),
            };

            desktop.MainWindow = splash;
            splash.Show();

            try
            {
                var splashVm = (SplashViewModel)splash.DataContext!;
                await AppHost.WarmupAsync(splashVm);

                var mainWindow = new MainWindow
                {
                    DataContext = AppHost.Services.GetRequiredService<MainWindowViewModel>(),
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                splash.Close();
            }
            catch (Exception ex)
            {
                var splashVm = (SplashViewModel)splash.DataContext!;
                splashVm.ErrorMessage = ex.Message;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
