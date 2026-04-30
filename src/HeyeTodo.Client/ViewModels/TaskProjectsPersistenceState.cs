using System.Collections.Generic;

namespace HeyeTodo.Client.ViewModels;

public sealed class TaskProjectsPersistenceState
{
    public int Version { get; set; } = 1;

    public List<TaskProjectPersistenceItem> Projects { get; set; } = new();
}
