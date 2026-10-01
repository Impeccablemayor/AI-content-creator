namespace Api.Contracts.Social;

/// <summary>
/// Public view of a connected social account.
/// </summary>
/// <remarks>
/// This contract intentionally has no access token, refresh token, or any
/// encrypted credential property. Tokens must never cross the API boundary.
/// </remarks>
public sealed class SocialAccountResponse
{
    public Guid Id { get; init; }

    public string Platform { get; init; } = string.Empty;

    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public string? ProfileUrl { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime? TokenExpiresAt { get; init; }

    public DateTime ConnectedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

public sealed class SocialConnectResponse
{
    public string Platform { get; init; } = string.Empty;

    public string AuthorizationUrl { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }
}
