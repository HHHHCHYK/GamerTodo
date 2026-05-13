using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(AccountViewModel account, TestPageViewModel testPage, TaskPanelViewModel taskPanel)
    {
        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("账号与同步", "◎", account),
            new("任务面板", "✓", taskPanel),
            new("测试界面", "●", testPage),
        };

        SelectNavigationItem(NavigationItems[1]);
    }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    [ObservableProperty]
    private ViewModelBase? _current;

    [RelayCommand]
    private void SelectNavigationItem(NavigationItemViewModel item)
    {
        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = false;
        }

        item.IsSelected = true;
        Current = item.Page;
    }
}
