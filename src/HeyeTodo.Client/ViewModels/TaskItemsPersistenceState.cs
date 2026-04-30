using System.Collections.Generic;

namespace HeyeTodo.Client.ViewModels;

public sealed class TaskItemsPersistenceState
{
    public int Version { get; set; } = 1;

    public List<TaskItemPersistenceItem> Tasks { get; set; } = new();
}
