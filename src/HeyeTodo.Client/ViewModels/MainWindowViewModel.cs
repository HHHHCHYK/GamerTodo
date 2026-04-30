using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(TestPageViewModel testPage, TaskPanelViewModel taskPanel)
    {
        NavigationItems = new ObservableCollection<NavigationItemViewModel>
        {
            new("测试界面", "●", testPage),
            new("任务面板", "✓", taskPanel),
        };

        SelectNavigationItem(NavigationItems[0]);
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
