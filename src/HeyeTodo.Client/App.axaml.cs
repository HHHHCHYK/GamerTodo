using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using HeyeTodo.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                AppHost.BootstrapCore();

                var vm = AppHost.Services.GetRequiredService<MainWindowViewModel>();
                vm.Current = AppHost.Services.GetRequiredService<TestPageViewModel>();

                var mainWindow = AppHost.Services.GetRequiredService<Views.MainWindow>();
                mainWindow.DataContext = vm;
                desktop.MainWindow = mainWindow;
            }
            catch (Exception ex)
            {
                desktop.MainWindow = CreateStartupErrorWindow(ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateStartupErrorWindow(Exception exception)
    {
        return new Window
        {
            Title = "HeyeTodo 启动失败",
            Width = 520,
            Height = 320,
            MinWidth = 420,
            MinHeight = 260,
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Spacing = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "HeyeTodo 启动失败",
                            FontSize = 24,
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                        },
                        new TextBlock
                        {
                            Text = "应用在初始化时遇到问题。请重新启动应用，或将下面的信息反馈给开发者。",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        },
                        new TextBox
                        {
                            Text = exception.Message,
                            IsReadOnly = true,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            MinHeight = 80,
                        },
                    },
                },
            },
        };
    }
}
