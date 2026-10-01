using Api.Domain.Enums;

namespace Api.Application.AnalyticsSync;

/// <summary>
/// The metrics a platform reported for one of its posts.
/// </summary>
/// <remarks>
/// Every value is nullable and the distinction is load bearing: null means the
/// platform did not report that metric, while 0 means it reported zero. The
/// snapshot column is nullable for the same reason, so a partial platform
/// response must not be padded with zeros.
/// </remarks>
public sealed record SocialMetricsSnapshot(
    long? Impressions = null,
    long? Likes = null,
    long? Comments = null,
    long? Shares = null,
    long? Clicks = null,
    long? ProfileVisits = null,
    long? FollowersGained = null);

/// <summary>
/// Reading performance metrics for a single social platform.
/// </summary>
/// <remarks>
/// Separate from <see cref="Publishing.ISocialPlatformPublisher"/> and from
/// <see cref="SocialAccounts.ISocialPlatformProvider"/>: a post is published
/// once and then measured on a schedule, so the three lifecycles are
/// independent. Metrics are read with the same access token that published the
/// post, which is why the external post id has to be stored at publish time.
/// </remarks>
public interface ISocialPlatformMetricsProvider
{
    ContentPlatform Platform { get; }

    /// <summary>
    /// False when the platform has no usable client configuration.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Reads the current metrics for a post the platform published.
    /// </summary>
    /// <remarks>
    /// Implementations must never include the access token or the provider
    /// response body in a thrown message, a log, or a returned value. Failures
    /// are reported through the shared
    /// <see cref="SocialAccounts.SocialProvider*Exception"/> types so a caller
    /// can tell a transient outage from rejected credentials.
    /// </remarks>
    Task<SocialMetricsSnapshot> FetchAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken);
}

public interface ISocialPlatformMetricsProviderResolver
{
    ISocialPlatformMetricsProvider Get(ContentPlatform platform);
}
