using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Networking;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly ClientSession _session;

    [ObservableProperty] private ViewModelBase? _content;
    [ObservableProperty] private NavItem? _selectedNav;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ShellViewModel(ClientSession session, ApiClient api)
    {
        _session = session;

        NavItems.Add(new NavItem("Nav.Tasks",      "\uE8FD", typeof(TaskListViewModel)));
        NavItems.Add(new NavItem("Nav.Gantt",      "\uE9D9", typeof(GanttViewModel)));
        NavItems.Add(new NavItem("Nav.MiniGames",  "\uE7FC", typeof(MiniGamesHubViewModel)));
        NavItems.Add(new NavItem("Nav.Settings",   "\uE713", typeof(SettingsViewModel)));

        // Choose initial view based on auth state.
        if (!api.IsAuthenticated)
        {
            Content = AppHost.Services.GetRequiredService<LoginViewModel>();
        }
        else
        {
            _session.IsAuthenticated = true;
            SelectedNav = NavItems[0];
        }
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is null) return;
        Content = (ViewModelBase)AppHost.Services.GetRequiredService(value.ViewModelType);
    }

    [RelayCommand]
    private void Logout()
    {
        var api = AppHost.Services.GetRequiredService<ApiClient>();
        api.Logout();
        _session.IsAuthenticated = false;
        Content = AppHost.Services.GetRequiredService<LoginViewModel>();
    }
}

public sealed record NavItem(string LabelKey, string Glyph, Type ViewModelType);
