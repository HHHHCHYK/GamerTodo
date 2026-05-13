using HeyeTodo.Client.ViewModels;

namespace HeyeTodo.Client.Persistence;

public sealed record TaskItemRecord(
    string Id,
    string ProjectId,
    string ProjectNameSnapshot,
    string Name,
    string Description,
    bool IsCompleted,
    int SortId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    string AssigneeName,
    TaskUrgencyLevel Urgency,
    long ServerVersion,
    DateTimeOffset? DeletedAt);
