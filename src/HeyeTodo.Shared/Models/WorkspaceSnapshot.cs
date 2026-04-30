using System.Collections.Generic;

namespace HeyeTodo.Shared.Models;

public sealed class WorkspaceSnapshot
{
    public int Version { get; set; } = 1;
    public UserProfileModel UserProfile { get; set; } = new();
    public List<ProjectModel> Projects { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}
