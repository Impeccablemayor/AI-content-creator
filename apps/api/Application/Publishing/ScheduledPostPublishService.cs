using Api.Application.Scheduling;
using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;

namespace Api.Application.Publishing;

public enum PublishOutcome
{
    /// <summary>The platform accepted the post and the post is now Published.</summary>
    Published = 1,

    /// <summary>The attempt failed. The post stays Scheduled and is retried.</summary>
    Failed = 2,

    /// <summary>The post was not publishable, so no platform call was made.</summary>
    Skipped = 3
}

public sealed record PublishAttemptResult(
    Guid ContentPostId,
    PublishOutcome Outcome,
    PublishFailureReason? FailureReason,
    string? ExternalPostId);

public interface IScheduledPostPublishService
{
    Task<PublishAttemptResult> PublishAsync(
        ScheduledPostCandidate candidate,
        DateTime claimedAt,
        CancellationToken cancellationToken);
}

/// <summary>
/// Publishes a claimed, due content post through the workspace's connected
/// social account.
/// </summary>
/// <remarks>
/// Failure never changes the post's status. The post stays Scheduled with a
/// recorded reason, and the existing claim expiry decides when it is attempted
/// again, so no separate retry subsystem is introduced.
/// </remarks>
public sealed class ScheduledPostPublishService(
    IScheduledPostStore store,
    ISocialPlatformPublisherResolver publisherResolver,
    ISocialCredentialProtector protector,
    ILogger<ScheduledPostPublishService> logger) : IScheduledPostPublishService
{
    public async Task<PublishAttemptResult> PublishAsync(
        ScheduledPostCandidate candidate,
        DateTime claimedAt,
        CancellationToken cancellationToken)
    {
        var postId = candidate.ContentPostId;
        var payload = await store.GetPublishPayloadAsync(postId, cancellationToken);
        if (payload is null)
        {
            logger.LogWarning(
                "Content post {ContentPostId} disappeared before it could be published.",
                postId);

            return Skipped(postId, PublishFailureReason.ContentPostNotScheduled);
        }

        // Defence in depth: the claim already requires Scheduled, but the final
        // decision is re-checked against the row actually loaded.
        if (payload.Status != ContentPostStatus.Scheduled)
        {
            logger.LogWarning(
                "Content post {ContentPostId} is {Status}, not Scheduled, so it was not published.",
                postId,
                payload.Status);

            return Skipped(postId, PublishFailureReason.ContentPostNotScheduled);
        }

        ISocialPlatformPublisher publisher;
        try
        {
            publisher = publisherResolver.Get(payload.Platform);
        }
        catch (UnsupportedSocialPlatformException)
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.PlatformNotSupported,
                $"no publisher exists for platform {payload.Platform}",
                cancellationToken);
        }

        if (!publisher.IsConfigured)
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.PlatformNotConfigured,
                $"platform {payload.Platform} has no client configuration",
                cancellationToken);
        }

        var accounts = await store.FindPublishAccountsAsync(
            payload.WorkspaceId,
            payload.Platform,
            cancellationToken);

        var selection = ConnectedSocialAccountSelector.Select(accounts, DateTime.UtcNow);
        if (!selection.HasAccount)
        {
            return await FailForAccountAsync(
                candidate,
                claimedAt,
                selection.Outcome,
                accounts.Count,
                payload,
                cancellationToken);
        }

        var account = selection.Account!;

        // The account query is already workspace scoped, so this cannot fail
        // today. It is checked because publishing to another workspace's account
        // is the worst possible outcome and must not pass silently.
        if (account.WorkspaceId != payload.WorkspaceId || account.Platform != payload.Platform)
        {
            logger.LogError(
                "Refusing to publish content post {ContentPostId} through social account " +
                "{SocialAccountId}, which is not owned by workspace {WorkspaceId} for " +
                "platform {Platform}.",
                postId,
                account.SocialAccountId,
                payload.WorkspaceId,
                payload.Platform);

            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.SocialAccountNotFound,
                "the resolved account is not owned by the post's workspace",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(account.AccessTokenEncrypted))
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.CredentialsUnavailable,
                $"social account {account.SocialAccountId} has no stored access token",
                cancellationToken);
        }

        if (account.TokenExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.CredentialsExpired,
                $"social account {account.SocialAccountId} expired at {expiresAt:O}",
                cancellationToken);
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
                "Stored credentials for social account {SocialAccountId} could not be " +
                "decrypted ({DecryptionFailure}). The account must be reconnected.",
                account.SocialAccountId,
                exception.GetType().Name);

            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.CredentialsUnavailable,
                $"stored credentials for social account {account.SocialAccountId} could not be decrypted",
                cancellationToken);
        }

        var text = SocialPostComposer.Compose(
            payload.Content,
            payload.CallToAction,
            payload.Hashtags);

        SocialPublishResult published;
        try
        {
            published = await publisher.PublishAsync(
                accessToken,
                new SocialPublishRequest(text, account.PlatformAccountId),
                cancellationToken);
        }
        catch (SocialProviderAuthenticationException)
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.CredentialsRejected,
                $"platform {payload.Platform} rejected the stored credentials",
                cancellationToken);
        }
        catch (SocialProviderUnavailableException)
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.ProviderUnavailable,
                $"platform {payload.Platform} was unavailable or rate limited",
                cancellationToken);
        }
        catch (SocialProviderRequestException)
        {
            return await FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.ProviderResponseInvalid,
                $"platform {payload.Platform} rejected the publish or returned an unreadable response",
                cancellationToken);
        }

        var publishedAt = DateTime.UtcNow;
        if (!await store.TryMarkPublishedAsync(
                postId,
                claimedAt,
                publishedAt,
                published.ExternalPostId,
                cancellationToken))
        {
            // The platform accepted the content, but this worker no longer owns
            // the post, so it must not write the result. The post stays
            // Scheduled and will be attempted again, which is the honest outcome
            // of an at-least-once publisher.
            logger.LogWarning(
                "Content post {ContentPostId} was published to {Platform} as external post " +
                "{ExternalPostId}, but its publish claim was lost before the status could be " +
                "recorded. The post remains Scheduled and may be published again.",
                postId,
                payload.Platform,
                published.ExternalPostId);

            return new PublishAttemptResult(
                postId,
                PublishOutcome.Failed,
                PublishFailureReason.PublishClaimLost,
                published.ExternalPostId);
        }

        logger.LogInformation(
            "Published content post {ContentPostId} for workspace {WorkspaceId} to {Platform} " +
            "as external post {ExternalPostId} via social account {SocialAccountId}.",
            postId,
            payload.WorkspaceId,
            payload.Platform,
            published.ExternalPostId,
            account.SocialAccountId);

        return new PublishAttemptResult(
            postId,
            PublishOutcome.Published,
            null,
            published.ExternalPostId);
    }

    private Task<PublishAttemptResult> FailForAccountAsync(
        ScheduledPostCandidate candidate,
        DateTime claimedAt,
        ConnectedAccountSelection selection,
        int accountCount,
        ScheduledPostPayload payload,
        CancellationToken cancellationToken) =>
        selection switch
        {
            ConnectedAccountSelection.Ambiguous => FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.SocialAccountAmbiguous,
                $"workspace {payload.WorkspaceId} has several connected {payload.Platform} accounts",
                cancellationToken),

            ConnectedAccountSelection.Expired => FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.CredentialsExpired,
                $"the {payload.Platform} account for workspace {payload.WorkspaceId} has expired credentials",
                cancellationToken),

            ConnectedAccountSelection.NotConnected => FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.SocialAccountNotConnected,
                $"workspace {payload.WorkspaceId} has no connected {payload.Platform} account",
                cancellationToken),

            _ => FailAsync(
                candidate,
                claimedAt,
                PublishFailureReason.SocialAccountNotFound,
                accountCount == 0
                    ? $"workspace {payload.WorkspaceId} has no {payload.Platform} account"
                    : $"workspace {payload.WorkspaceId} has no usable {payload.Platform} account",
                cancellationToken)
        };

    private async Task<PublishAttemptResult> FailAsync(
        ScheduledPostCandidate candidate,
        DateTime claimedAt,
        PublishFailureReason reason,
        string detail,
        CancellationToken cancellationToken)
    {
        var recorded = await store.TryRecordPublishFailureAsync(
            candidate.ContentPostId,
            claimedAt,
            reason,
            DateTime.UtcNow,
            cancellationToken);

        logger.LogWarning(
            "Publishing content post {ContentPostId} for workspace {WorkspaceId} on " +
            "{Platform} failed with {FailureReason}: {Detail}. The post remains Scheduled " +
            "and will be retried when its claim expires. Failure recorded: {FailureRecorded}.",
            candidate.ContentPostId,
            candidate.WorkspaceId,
            candidate.Platform,
            reason,
            detail,
            recorded);

        return recorded
            ? new PublishAttemptResult(candidate.ContentPostId, PublishOutcome.Failed, reason, null)
            : new PublishAttemptResult(
                candidate.ContentPostId,
                PublishOutcome.Failed,
                PublishFailureReason.PublishClaimLost,
                null);
    }

    private static PublishAttemptResult Skipped(
        Guid contentPostId,
        PublishFailureReason reason) =>
        new(contentPostId, PublishOutcome.Skipped, reason, null);
}
