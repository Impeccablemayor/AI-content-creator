using Api.Domain.Enums;

namespace Api.Application.Scheduling;

public sealed record ScheduledPostCandidate(
    Guid ContentPostId,
    Guid WorkspaceId,
    Guid ContentIdeaId,
    ContentPlatform Platform,
    ContentPostStatus Status,
    DateTime? ScheduledAt,
    DateTime? PublishClaimedAt);

public static class ScheduledPostClaimPolicy
{
    public static TimeSpan ResolveClaimExpiry(TimeSpan? configuredExpiry) =>
        configuredExpiry is { } expiry && expiry > TimeSpan.Zero
            ? expiry
            : TimeSpan.FromMinutes(15);

    public static bool IsDue(ScheduledPostCandidate candidate, DateTime now) =>
        candidate.Status == ContentPostStatus.Scheduled &&
        candidate.ScheduledAt is not null &&
        candidate.ScheduledAt <= now;

    public static bool CanBeClaimed(
        ScheduledPostCandidate candidate,
        DateTime now,
        TimeSpan claimExpiry) =>
        candidate.PublishClaimedAt is null ||
        candidate.PublishClaimedAt <= now - claimExpiry;
}
