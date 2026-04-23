using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly ClientSession _session;
    private readonly ApiClient _api;
    private readonly INavigationService _navigation;
    private readonly IServiceProvider _services;

    [ObservableProperty] private ViewModelBase? _content;
    [ObservableProperty] private NavItem? _selectedNav;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public ShellViewModel(
        ClientSession session,
        ApiClient api,
        INavigationService navigation,
        IServiceProvider services)
    {
        _session = session;
        _api = api;
        _navigation = navigation;
        _services = services;

        NavItems.Add(new NavItem("Nav.Tasks",     "M4 6H16V8H4z M4 10H16V12H4z M4 14H16V16H4z M2 6H3.5V8H2z M2 10H3.5V12H2z M2 14H3.5V16H2z", typeof(TaskListViewModel)));
        NavItems.Add(new NavItem("Nav.Gantt",     "M2 15H18V17H2z M3 11H7V14H3z M8 7H12V14H8z M13 4H17V14H13z", typeof(GanttViewModel)));
        NavItems.Add(new NavItem("Nav.MiniGames", "M7 7H13V9H15V7H17V11H15V13H13V11H11V13H9V11H7z M4 12A1.5 1.5 0 1 0 4 15A1.5 1.5 0 1 0 4 12 M16 12A1.5 1.5 0 1 0 16 15A1.5 1.5 0 1 0 16 12 M5 6H15A3 3 0 0 1 18 9V13A3 3 0 0 1 15 16H5A3 3 0 0 1 2 13V9A3 3 0 0 1 5 6z", typeof(MiniGamesHubViewModel)));
        NavItems.Add(new NavItem("Nav.Settings",  "M10 2L11 4.2L13.4 4.6L14.6 7L17 8L16.2 10.5L17 13L14.6 14L13.4 16.4L11 16.8L10 19L8.9 16.8L6.6 16.4L5.4 14L3 13L3.8 10.5L3 8L5.4 7L6.6 4.6L8.9 4.2z M10 7A3 3 0 1 0 10 13A3 3 0 1 0 10 7z", typeof(SettingsViewModel)));

        // Choose initial view based on auth state.
        if (!api.IsAuthenticated)
        {
            _navigation.NavigateTo<LoginViewModel>();
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
        Content = (ViewModelBase)_services.GetRequiredService(value.ViewModelType);
    }

    [RelayCommand]
    private async Task Logout()
    {
        await _api.LogoutAsync();
        _session.Reset();
        _navigation.NavigateTo<LoginViewModel>();
    }
}

public sealed record NavItem(string LabelKey, string IconPathData, Type ViewModelType)
{
    public string Label => LocalizationService.Instance[LabelKey];
}
