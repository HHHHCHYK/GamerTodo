namespace HeyeTodo.Client.ViewModels;

public sealed class TaskUrgencyOptionViewModel : ViewModelBase
{
    public TaskUrgencyOptionViewModel(TaskUrgencyLevel value, string name)
    {
        Value = value;
        Name = name;
    }

    public TaskUrgencyLevel Value { get; }

    public string Name { get; }
}
