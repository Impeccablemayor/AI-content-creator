using Api.Application.Scheduling;
using Api.Domain.Enums;

namespace Api.Tests;

/// <summary>
/// A store for service tests that never exercise scheduling. Its methods throw,
/// so any accidental store call fails the test loudly instead of silently
/// scheduling nothing.
/// </summary>
internal sealed class NotSchedulingScheduledPostStore : IScheduledPostStore
{
    public Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
        DateTime now,
        int limit,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryClaimAsync(
        Guid contentPostId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryClaimForImmediatePublishAsync(
        Guid contentPostId,
        Guid workspaceId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryCancelScheduleAsync(
        Guid contentPostId,
        Guid workspaceId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryScheduleAsync(
        Guid contentPostId,
        Guid workspaceId,
        DateTime scheduledAt,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ScheduledPostPayload?> GetPublishPayloadAsync(
        Guid contentPostId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
        Guid workspaceId,
        ContentPlatform platform,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryMarkPublishedAsync(
        Guid contentPostId,
        DateTime claimedAt,
        DateTime publishedAt,
        string? externalPostId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryRecordPublishFailureAsync(
        Guid contentPostId,
        DateTime claimedAt,
        PublishFailureReason reason,
        DateTime failedAt,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>
/// A store for tests that schedule but do not care about the schedule write
/// itself. The schedule always succeeds; every other store method throws.
/// </summary>
internal sealed class SchedulingSucceedsScheduledPostStore : IScheduledPostStore
{
    public Task<bool> TryScheduleAsync(
        Guid contentPostId,
        Guid workspaceId,
        DateTime scheduledAt,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
        DateTime now,
        int limit,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryClaimAsync(
        Guid contentPostId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryClaimForImmediatePublishAsync(
        Guid contentPostId,
        Guid workspaceId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryCancelScheduleAsync(
        Guid contentPostId,
        Guid workspaceId,
        DateTime now,
        TimeSpan claimExpiry,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ScheduledPostPayload?> GetPublishPayloadAsync(
        Guid contentPostId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
        Guid workspaceId,
        ContentPlatform platform,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryMarkPublishedAsync(
        Guid contentPostId,
        DateTime claimedAt,
        DateTime publishedAt,
        string? externalPostId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> TryRecordPublishFailureAsync(
        Guid contentPostId,
        DateTime claimedAt,
        PublishFailureReason reason,
        DateTime failedAt,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}