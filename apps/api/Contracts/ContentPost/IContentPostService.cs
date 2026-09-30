using Api.Domain.Enums;

namespace Api.Contracts.ContentPost;

public interface IContentPostService
{
    Task<ContentPostResponse?> GenerateFromIdeaAsync(
        Guid contentIdeaId,
        GenerateContentPostRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentPostResponse>> GetAsync(
        ContentPostStatus? status,
        ContentPlatform? platform,
        CancellationToken cancellationToken = default);

    Task<ContentPostResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentPostResponse?> UpdateAsync(
        Guid id,
        UpdateContentPostRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentPostResponse?> RegenerateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentPostResponse?> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentPostResponse?> RejectAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
