namespace HeyeTodo.Client.Persistence;

public sealed record TaskWorkspaceState(
    IReadOnlyList<TaskProjectRecord> Projects,
    IReadOnlyList<TaskItemRecord> Tasks);
