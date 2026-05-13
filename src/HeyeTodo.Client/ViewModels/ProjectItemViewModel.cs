using CommunityToolkit.Mvvm.ComponentModel;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class ProjectItemViewModel : ViewModelBase
{
    public ProjectItemViewModel(string name, string description)
        : this(Guid.NewGuid().ToString("D"), name, description)
    {
    }

    public ProjectItemViewModel(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public string Id { get; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public long ServerVersion { get; set; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;
}
