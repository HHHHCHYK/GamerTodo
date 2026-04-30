using System.Collections.Generic;

namespace HeyeTodo.Shared.Models;

public sealed class ProjectModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TaskModel> Tasks { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}
