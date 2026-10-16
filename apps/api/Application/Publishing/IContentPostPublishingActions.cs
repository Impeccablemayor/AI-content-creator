using Api.Contracts.ContentPost;

namespace Api.Application.Publishing;

public interface IContentPostPublishingActions
{
    Task<PublishNowResponse?> PublishNowAsync(
        Guid contentPostId,
        CancellationToken cancellationToken);

    Task<ContentPostResponse?> CancelScheduleAsync(
        Guid contentPostId,
        CancellationToken cancellationToken);
}
