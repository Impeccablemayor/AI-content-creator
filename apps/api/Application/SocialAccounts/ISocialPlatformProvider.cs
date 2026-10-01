using Api.Domain.Enums;

namespace Api.Application.SocialAccounts;

public sealed record SocialTokenResponse(
    string AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    IReadOnlyList<string> Scopes);

public sealed record SocialAccountProfile(
    string PlatformAccountId,
    string? Username,
    string? DisplayName,
    string? ProfileUrl);

/// <summary>
/// OAuth concerns for a single social platform.
/// </summary>
/// <remarks>
/// Deliberately limited to connection management. Publishing, analytics, and
/// token refresh are separate concerns and are not part of this abstraction.
/// </remarks>
public interface ISocialPlatformProvider
{
    ContentPlatform Platform { get; }

    /// <summary>
    /// False when the platform has no usable client configuration. A
    /// non-configured provider must fail the flow rather than issue a URL that
    /// cannot complete.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Builds the provider authorization URL. The redirect URI always comes
    /// from configuration and is never taken from the request.
    /// </summary>
    Uri BuildAuthorizationUri(string state, string? codeChallenge);

    Task<SocialTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string? codeVerifier,
        CancellationToken cancellationToken);

    Task<SocialAccountProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken);
}

public interface ISocialPlatformProviderResolver
{
    ISocialPlatformProvider Get(ContentPlatform platform);
}
