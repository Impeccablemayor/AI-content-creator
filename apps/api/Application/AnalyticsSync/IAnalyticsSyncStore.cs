using Api.Application.SocialAccounts;
using Api.Domain.Enums;

namespace Api.Application.AnalyticsSync;

public interface IAnalyticsSyncStore
{
    /// <summary>
    /// Published posts whose metrics are due for a resync, oldest publication
    /// first, so a post that has waited longest is measured first.
    /// </summary>
    Task<IReadOnlyList<MetricsSyncCandidate>> FindDueAsync(
        DateTime dueBefore,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Connected social accounts for a workspace and platform, newest first.
    /// Scoping the query to the workspace is what enforces metric ownership.
    /// </summary>
    Task<IReadOnlyList<MetricsSyncAccountCandidate>> FindAccountsAsync(
        Guid workspaceId,
        ContentPlatform platform,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that an attempt was made, whether or not it succeeded. This is
    /// what spaces out retries for a post the platform will never report on.
    /// </summary>
    Task<bool> TryRecordAttemptAsync(
        Guid contentPostId,
        DateTime attemptedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new snapshot. A post may have several, because platform
    /// statistics keep moving; readers take the most recent.
    /// </summary>
    Task AddSnapshotAsync(
        ContentPostMetricSnapshot snapshot,
        CancellationToken cancellationToken);
}

/// <summary>A published post that needs its metrics read.</summary>
public sealed record MetricsSyncCandidate(
    Guid ContentPostId,
    Guid WorkspaceId,
    ContentPlatform Platform,
    string ExternalPostId,
    DateTime? PublishedAt);

/// <summary>
/// A social account metrics could be read through. Carries only what the metrics
/// providers need; the access token is still encrypted at rest.
/// </summary>
public sealed record MetricsSyncAccountCandidate(
    Guid SocialAccountId,
    Guid WorkspaceId,
    ContentPlatform Platform,
    SocialAccountStatus Status,
    string? AccessTokenEncrypted,
    DateTime? TokenExpiresAt) : ISocialAccountCandidate;

/// <summary>The measurements to persist for one post.</summary>
public sealed record ContentPostMetricSnapshot(
    Guid WorkspaceId,
    Guid ContentPostId,
    long? Impressions,
    long? Likes,
    long? Comments,
    long? Shares,
    long? Clicks,
    long? ProfileVisits,
    long? FollowersGained,
    DateTime RecordedAt);

public interface IAnalyticsSyncProcessor
{
    Task<AnalyticsSyncRunResult> RunOnceAsync(CancellationToken cancellationToken);
}

public sealed record AnalyticsSyncRunResult(
    int DueFound,
    int Synced,
    int Skipped,
    int Failed = 0);
