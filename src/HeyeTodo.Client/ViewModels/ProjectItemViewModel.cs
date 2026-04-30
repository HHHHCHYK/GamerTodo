using CommunityToolkit.Mvvm.ComponentModel;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class ProjectItemViewModel : ViewModelBase
{
    public ProjectItemViewModel(string name, string description)
        : this(Guid.NewGuid().ToString("N"), name, description)
    {
    }

    public ProjectItemViewModel(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public string Id { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;
}
