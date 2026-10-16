using Api.Domain.Enums;

namespace Api.Application.ContentPosts;

public static class ContentPostStatusTransitions
{
    public static bool CanEdit(ContentPostStatus status) =>
        status == ContentPostStatus.Draft;

    public static bool CanRegenerate(ContentPostStatus status) =>
        status is ContentPostStatus.Draft or ContentPostStatus.Rejected;

    public static bool CanApprove(ContentPostStatus status) =>
        status == ContentPostStatus.Draft;

    public static bool CanReject(ContentPostStatus status) =>
        status == ContentPostStatus.Draft;

    public static bool CanSchedule(ContentPostStatus status) =>
        status is ContentPostStatus.Approved or ContentPostStatus.Scheduled;

    public static bool CanPublishImmediately(ContentPostStatus status) =>
        status is ContentPostStatus.Approved or ContentPostStatus.Scheduled;

    public static bool CanCancelSchedule(ContentPostStatus status) =>
        status == ContentPostStatus.Scheduled;

    /// <summary>
    /// Only a scheduled post may be published. Publishing is driven by the
    /// worker, so this is the single gate that keeps a draft, approved,
    /// rejected, or already published post from being sent to a platform.
    /// </summary>
    public static bool CanPublish(ContentPostStatus status) =>
        status == ContentPostStatus.Scheduled;
}
