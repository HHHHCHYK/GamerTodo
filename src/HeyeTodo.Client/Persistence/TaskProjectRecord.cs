namespace HeyeTodo.Client.Persistence;

public sealed record TaskProjectRecord(
    string Id,
    string Name,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long ServerVersion,
    DateTimeOffset? DeletedAt);
