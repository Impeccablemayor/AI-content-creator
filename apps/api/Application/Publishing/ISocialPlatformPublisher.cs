using Api.Domain.Enums;

namespace Api.Application.Publishing;

/// <summary>
/// One publish attempt.
/// </summary>
/// <param name="Text">The composed text to publish.</param>
/// <param name="PlatformAccountId">
/// The connected account's platform identifier, as stored during the connection
/// flow. Some publish APIs require it in the request body rather than deriving
/// the account from the token.
/// </param>
public sealed record SocialPublishRequest(string Text, string PlatformAccountId);

/// <summary>
/// A confirmed publish, carrying the platform's own identifier for the created
/// post and a link to it when the platform exposes one.
/// </summary>
public sealed record SocialPublishResult(string ExternalPostId, string? Url);

/// <summary>
/// Publishing for a single social platform.
/// </summary>
/// <remarks>
/// Separate from <see cref="SocialAccounts.ISocialPlatformProvider"/>, which owns
/// connection management. A workspace connects an account once and publishes
/// through it many times, so the two lifecycles are independent.
/// </remarks>
public interface ISocialPlatformPublisher
{
    ContentPlatform Platform { get; }

    /// <summary>
    /// False when the platform has no usable client configuration.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Publishes text as the account the access token belongs to.
    /// </summary>
    /// <remarks>
    /// Implementations must never include the access token or the provider
    /// response body in a thrown message, a log, or a returned value.
    /// </remarks>
    Task<SocialPublishResult> PublishAsync(
        string accessToken,
        SocialPublishRequest request,
        CancellationToken cancellationToken);
}

public interface ISocialPlatformPublisherResolver
{
    ISocialPlatformPublisher Get(ContentPlatform platform);
}
