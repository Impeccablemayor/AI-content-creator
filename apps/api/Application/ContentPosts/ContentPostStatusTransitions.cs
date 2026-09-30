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
}
