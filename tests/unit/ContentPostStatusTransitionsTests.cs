using Api.Application.ContentPosts;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostStatusTransitionsTests
{
    [Theory]
    [InlineData(ContentPostStatus.Draft, true)]
    [InlineData(ContentPostStatus.Approved, false)]
    [InlineData(ContentPostStatus.Scheduled, false)]
    [InlineData(ContentPostStatus.Published, false)]
    [InlineData(ContentPostStatus.Rejected, false)]
    public void Only_drafts_can_be_edited(ContentPostStatus status, bool expected) =>
        Assert.Equal(expected, ContentPostStatusTransitions.CanEdit(status));

    [Theory]
    [InlineData(ContentPostStatus.Draft, true)]
    [InlineData(ContentPostStatus.Rejected, true)]
    [InlineData(ContentPostStatus.Approved, false)]
    [InlineData(ContentPostStatus.Scheduled, false)]
    [InlineData(ContentPostStatus.Published, false)]
    public void Drafts_and_rejected_posts_can_be_regenerated(
        ContentPostStatus status,
        bool expected) =>
        Assert.Equal(expected, ContentPostStatusTransitions.CanRegenerate(status));

    [Theory]
    [InlineData(ContentPostStatus.Draft, true)]
    [InlineData(ContentPostStatus.Approved, false)]
    [InlineData(ContentPostStatus.Scheduled, false)]
    [InlineData(ContentPostStatus.Published, false)]
    [InlineData(ContentPostStatus.Rejected, false)]
    public void Only_drafts_can_be_approved(ContentPostStatus status, bool expected) =>
        Assert.Equal(expected, ContentPostStatusTransitions.CanApprove(status));

    [Theory]
    [InlineData(ContentPostStatus.Draft, true)]
    [InlineData(ContentPostStatus.Approved, false)]
    [InlineData(ContentPostStatus.Scheduled, false)]
    [InlineData(ContentPostStatus.Published, false)]
    [InlineData(ContentPostStatus.Rejected, false)]
    public void Only_drafts_can_be_rejected(ContentPostStatus status, bool expected) =>
        Assert.Equal(expected, ContentPostStatusTransitions.CanReject(status));

    /// <summary>
    /// Only a scheduled post is publishable, and publishing is terminal: a
    /// published post is never published again.
    /// </summary>
    [Theory]
    [InlineData(ContentPostStatus.Draft, false)]
    [InlineData(ContentPostStatus.Approved, false)]
    [InlineData(ContentPostStatus.Scheduled, true)]
    [InlineData(ContentPostStatus.Published, false)]
    [InlineData(ContentPostStatus.Rejected, false)]
    public void Only_scheduled_posts_can_be_published(ContentPostStatus status, bool expected) =>
        Assert.Equal(expected, ContentPostStatusTransitions.CanPublish(status));
}
