using CommunityToolkit.Mvvm.ComponentModel;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _current;

    public MainWindowViewModel(ShellViewModel shell)
    {
        _current = shell;
    }
}
