using Api.Application.SocialAccounts;
using Api.Domain.Enums;

namespace Api.Application.Scheduling;

public interface IScheduledPostStore
{
    Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
        DateTime now,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> TryClaimAsync(
        Guid contentPostId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the publishable text for a claimed post, or null when the post no
    /// longer exists.
    /// </summary>
    Task<ScheduledPostPayload?> GetPublishPayloadAsync(
        Guid contentPostId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Connected social accounts for a workspace and platform, newest first.
    /// Scoping the query to the workspace is what enforces publish ownership.
    /// </summary>
    Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
        Guid workspaceId,
        ContentPlatform platform,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a claimed post published, keeping the platform's own post id so
    /// metrics can be fetched for it later. Returns false when the claim or the
    /// status no longer matches, meaning this worker no longer owns the post.
    /// </summary>
    Task<bool> TryMarkPublishedAsync(
        Guid contentPostId,
        DateTime claimedAt,
        DateTime publishedAt,
        string? externalPostId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records why a publish attempt failed. The claim is left in place so the
    /// existing claim expiry, not a new mechanism, spaces out the next attempt.
    /// Returns false when the claim or status no longer matches.
    /// </summary>
    Task<bool> TryRecordPublishFailureAsync(
        Guid contentPostId,
        DateTime claimedAt,
        PublishFailureReason reason,
        DateTime failedAt,
        CancellationToken cancellationToken);
}

public interface IScheduledPostProcessor
{
    Task<ScheduledPostRunResult> RunOnceAsync(CancellationToken cancellationToken);
}

public sealed record ScheduledPostRunResult(
    int DueFound,
    int Claimed,
    int SkippedAlreadyClaimed,
    int Published = 0,
    int Failed = 0);

/// <summary>The parts of a post that become the published text.</summary>
public sealed record ScheduledPostPayload(
    Guid ContentPostId,
    Guid WorkspaceId,
    ContentPlatform Platform,
    ContentPostStatus Status,
    string Content,
    string CallToAction,
    IReadOnlyList<string> Hashtags);

/// <summary>
/// A social account a post could be published through. Carries only what the
/// publisher needs; the access token is still encrypted at rest.
/// </summary>
public sealed record PublishAccountCandidate(
    Guid SocialAccountId,
    Guid WorkspaceId,
    ContentPlatform Platform,
    string PlatformAccountId,
    SocialAccountStatus Status,
    string? AccessTokenEncrypted,
    DateTime? TokenExpiresAt) : ISocialAccountCandidate;
