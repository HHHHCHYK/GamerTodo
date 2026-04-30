using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class NavigationItemViewModel : ViewModelBase
{
    public NavigationItemViewModel(string title, string icon, ViewModelBase page)
    {
        Title = title;
        Icon = icon;
        Page = page;
    }

    public string Title { get; }

    public string Icon { get; }

    public ViewModelBase Page { get; }

    [ObservableProperty]
    private bool _isSelected;
}
