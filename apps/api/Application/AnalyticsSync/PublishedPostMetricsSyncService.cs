using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;

namespace Api.Application.AnalyticsSync;

public enum MetricsSyncOutcome
{
    /// <summary>Metrics were read and a snapshot was stored.</summary>
    Synced = 1,

    /// <summary>Nothing was measured because this post cannot be measured.</summary>
    Skipped = 2,

    /// <summary>The read failed. No snapshot was written.</summary>
    Failed = 3
}

/// <summary>Why a metrics read produced no snapshot.</summary>
public enum MetricsSyncFailureReason
{
    PlatformNotSupported = 1,
    PlatformNotConfigured = 2,
    SocialAccountNotFound = 3,
    SocialAccountNotConnected = 4,
    SocialAccountAmbiguous = 5,
    CredentialsExpired = 6,
    CredentialsUnavailable = 7,
    CredentialsRejected = 8,
    ProviderUnavailable = 9,
    ProviderResponseInvalid = 10
}

public sealed record MetricsSyncAttemptResult(
    Guid ContentPostId,
    MetricsSyncOutcome Outcome,
    MetricsSyncFailureReason? FailureReason,
    string? Detail);

public interface IPublishedPostMetricsSyncService
{
    Task<MetricsSyncAttemptResult> SyncAsync(
        MetricsSyncCandidate candidate,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reads the current metrics for one published post and stores a snapshot.
/// </summary>
/// <remarks>
/// <para>
/// The attempt is recorded before the platform is called, so every outcome,
/// including a platform that can never report on the post, waits for the next
/// resync interval instead of being retried on every poll. A snapshot is only
/// written after a successful read, so a failure never looks like a zero.
/// </para>
/// <para>
/// The access token that published the post is reused, which is why the
/// platform's post id has to be stored at publish time. Nothing here publishes.
/// </para>
/// </remarks>
public sealed class PublishedPostMetricsSyncService(
    IAnalyticsSyncStore store,
    ISocialPlatformMetricsProviderResolver providerResolver,
    ISocialCredentialProtector protector,
    ILogger<PublishedPostMetricsSyncService> logger) : IPublishedPostMetricsSyncService
{
    public async Task<MetricsSyncAttemptResult> SyncAsync(
        MetricsSyncCandidate candidate,
        CancellationToken cancellationToken)
    {
        var postId = candidate.ContentPostId;

        // Recorded first so a provider outage, a misconfiguration and a
        // permanent "no data" answer all wait for the next resync rather than
        // being retried on every poll.
        if (!await store.TryRecordAttemptAsync(postId, DateTime.UtcNow, cancellationToken))
        {
            logger.LogInformation(
                "Content post {ContentPostId} is no longer eligible for metrics sync.",
                postId);

            return Skipped(postId, null, "the post is no longer published with a platform post id");
        }

        ISocialPlatformMetricsProvider provider;
        try
        {
            provider = providerResolver.Get(candidate.Platform);
        }
        catch (UnsupportedSocialPlatformException)
        {
            return Skipped(
                postId,
                MetricsSyncFailureReason.PlatformNotSupported,
                $"no metrics provider exists for platform {candidate.Platform}");
        }

        if (!provider.IsConfigured)
        {
            return Skipped(
                postId,
                MetricsSyncFailureReason.PlatformNotConfigured,
                $"platform {candidate.Platform} has no client configuration");
        }

        var accounts = await store.FindAccountsAsync(
            candidate.WorkspaceId,
            candidate.Platform,
            cancellationToken);

        var selection = ConnectedSocialAccountSelector.Select(accounts, DateTime.UtcNow);
        if (!selection.HasAccount)
        {
            return Fail(
                candidate,
                ReasonFor(selection.Outcome),
                DetailFor(selection.Outcome, candidate));
        }

        var account = selection.Account!;

        // The account query is already workspace scoped, so this cannot fail
        // today. It is checked because reading another workspace's performance
        // is a data leak and must not pass silently.
        if (account.WorkspaceId != candidate.WorkspaceId || account.Platform != candidate.Platform)
        {
            logger.LogError(
                "Refusing to read metrics for content post {ContentPostId} through social account " +
                "{SocialAccountId}, which is not owned by workspace {WorkspaceId} for platform " +
                "{Platform}.",
                postId,
                account.SocialAccountId,
                candidate.WorkspaceId,
                candidate.Platform);

            return Fail(
                candidate,
                MetricsSyncFailureReason.SocialAccountNotFound,
                "the resolved account is not owned by the post's workspace");
        }

        if (string.IsNullOrWhiteSpace(account.AccessTokenEncrypted))
        {
            return Fail(
                candidate,
                MetricsSyncFailureReason.CredentialsUnavailable,
                $"social account {account.SocialAccountId} has no stored access token");
        }

        if (account.TokenExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
        {
            return Fail(
                candidate,
                MetricsSyncFailureReason.CredentialsExpired,
                $"social account {account.SocialAccountId} expired at {expiresAt:O}");
        }

        string accessToken;
        try
        {
            accessToken = protector.UnprotectAccessToken(account.AccessTokenEncrypted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A key ring that no longer matches the stored ciphertext, for
            // example. The exception may describe key material, so it is
            // reported by type only and never logged.
            logger.LogWarning(
                "Stored credentials for social account {SocialAccountId} could not be decrypted " +
                "({DecryptionFailure}). The account must be reconnected.",
                account.SocialAccountId,
                exception.GetType().Name);

            return Fail(
                candidate,
                MetricsSyncFailureReason.CredentialsUnavailable,
                $"stored credentials for social account {account.SocialAccountId} could not be decrypted");
        }

        SocialMetricsSnapshot metrics;
        try
        {
            metrics = await provider.FetchAsync(
                accessToken,
                candidate.ExternalPostId,
                cancellationToken);
        }
        catch (SocialProviderAuthenticationException)
        {
            return Fail(
                candidate,
                MetricsSyncFailureReason.CredentialsRejected,
                $"platform {candidate.Platform} rejected the stored credentials");
        }
        catch (SocialProviderUnavailableException)
        {
            return Fail(
                candidate,
                MetricsSyncFailureReason.ProviderUnavailable,
                $"platform {candidate.Platform} was unavailable or rate limited");
        }
        catch (SocialProviderRequestException)
        {
            return Fail(
                candidate,
                MetricsSyncFailureReason.ProviderResponseInvalid,
                $"platform {candidate.Platform} rejected the metrics request or returned an unreadable response");
        }

        await store.AddSnapshotAsync(
            new ContentPostMetricSnapshot(
                candidate.WorkspaceId,
                postId,
                metrics.Impressions,
                metrics.Likes,
                metrics.Comments,
                metrics.Shares,
                metrics.Clicks,
                metrics.ProfileVisits,
                metrics.FollowersGained,
                DateTime.UtcNow),
            cancellationToken);

        logger.LogInformation(
            "Recorded a metrics snapshot for content post {ContentPostId} of workspace " +
            "{WorkspaceId} on {Platform}, published at {PublishedAt}.",
            postId,
            candidate.WorkspaceId,
            candidate.Platform,
            candidate.PublishedAt);

        return new MetricsSyncAttemptResult(postId, MetricsSyncOutcome.Synced, null, null);
    }

    private static MetricsSyncFailureReason ReasonFor(ConnectedAccountSelection selection) =>
        selection switch
        {
            ConnectedAccountSelection.Ambiguous => MetricsSyncFailureReason.SocialAccountAmbiguous,
            ConnectedAccountSelection.Expired => MetricsSyncFailureReason.CredentialsExpired,
            ConnectedAccountSelection.NotConnected => MetricsSyncFailureReason.SocialAccountNotConnected,
            _ => MetricsSyncFailureReason.SocialAccountNotFound
        };

    private static string DetailFor(
        ConnectedAccountSelection selection,
        MetricsSyncCandidate candidate) =>
        selection switch
        {
            ConnectedAccountSelection.Ambiguous =>
                $"workspace {candidate.WorkspaceId} has several connected {candidate.Platform} accounts",
            ConnectedAccountSelection.Expired =>
                $"the {candidate.Platform} account for workspace {candidate.WorkspaceId} has expired credentials",
            ConnectedAccountSelection.NotConnected =>
                $"workspace {candidate.WorkspaceId} has no connected {candidate.Platform} account",
            _ =>
                $"workspace {candidate.WorkspaceId} has no usable {candidate.Platform} account"
        };

    private MetricsSyncAttemptResult Fail(
        MetricsSyncCandidate candidate,
        MetricsSyncFailureReason reason,
        string detail)
    {
        logger.LogWarning(
            "Reading metrics for content post {ContentPostId} of workspace {WorkspaceId} on " +
            "{Platform} failed with {FailureReason}: {Detail}. No snapshot was recorded and " +
            "the post will be retried after the next resync interval.",
            candidate.ContentPostId,
            candidate.WorkspaceId,
            candidate.Platform,
            reason,
            detail);

        return new MetricsSyncAttemptResult(
            candidate.ContentPostId,
            MetricsSyncOutcome.Failed,
            reason,
            detail);
    }

    private static MetricsSyncAttemptResult Skipped(
        Guid contentPostId,
        MetricsSyncFailureReason? reason,
        string detail) =>
        new(contentPostId, MetricsSyncOutcome.Skipped, reason, detail);
}
