using CommunityToolkit.Mvvm.ComponentModel;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class ProjectFilterViewModel : ViewModelBase
{
    public ProjectFilterViewModel(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    [ObservableProperty]
    private string _name;
}
