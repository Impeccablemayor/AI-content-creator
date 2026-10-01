using Api.Application.ContentPosts;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostSchedulingTransitionsTests
{
    [Theory]
    [InlineData(ContentPostStatus.Approved, true)]
    [InlineData(ContentPostStatus.Scheduled, true)]
    [InlineData(ContentPostStatus.Draft, false)]
    [InlineData(ContentPostStatus.Rejected, false)]
    [InlineData(ContentPostStatus.Published, false)]
    public void Only_approved_and_scheduled_posts_can_be_scheduled(
        ContentPostStatus status,
        bool expected) =>
        Assert.Equal(expected, ContentPostStatusTransitions.CanSchedule(status));

    [Fact]
    public void Scheduling_never_opens_up_the_review_transitions()
    {
        foreach (var status in Enum.GetValues<ContentPostStatus>())
        {
            var canEdit = ContentPostStatusTransitions.CanEdit(status);
            var canApprove = ContentPostStatusTransitions.CanApprove(status);
            var canReject = ContentPostStatusTransitions.CanReject(status);
            var canRegenerate = ContentPostStatusTransitions.CanRegenerate(status);
            var canSchedule = ContentPostStatusTransitions.CanSchedule(status);

            Assert.True(canEdit == (status == ContentPostStatus.Draft));
            Assert.True(canApprove == (status == ContentPostStatus.Draft));
            Assert.True(canReject == (status == ContentPostStatus.Draft));
            Assert.True(canRegenerate == (status is ContentPostStatus.Draft or ContentPostStatus.Rejected));

            if (status is ContentPostStatus.Scheduled or ContentPostStatus.Published)
            {
                Assert.False(canEdit);
                Assert.False(canApprove);
                Assert.False(canReject);
                Assert.False(canRegenerate);
            }

            if (status == ContentPostStatus.Published)
            {
                Assert.False(canSchedule);
            }
        }
    }
}
