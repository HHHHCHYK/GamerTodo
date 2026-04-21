namespace HeyeTodo.Server.Infrastructure.Auth;

/// <summary>Options bound from <c>Jwt</c> section.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "HeyeTodo";
    public string Audience { get; set; } = "HeyeTodo.Client";

    /// <summary>Symmetric signing key. Must be >= 32 bytes in production.</summary>
    public string SigningKey { get; set; } = null!;

    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}
