namespace HeyeTodo.Client.Services;

public sealed record ClientSession(
    Guid ClientId,
    string? ServerBaseUrl,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiresAt)
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(ServerBaseUrl);
}
