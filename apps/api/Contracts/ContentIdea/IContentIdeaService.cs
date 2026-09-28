using Api.Contracts.ContentIdea;
using Api.Domain.Enums;

namespace Api.Application.ContentIdeas;

public interface IContentIdeaService
{
    Task<IReadOnlyList<ContentIdeaResponse>> GetAsync(
        ContentIdeaStatus? status,
        ContentPlatform? platform,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentIdeaResponse>> GenerateAsync(
        GenerateContentIdeasRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentIdeaResponse?> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentIdeaResponse?> RejectAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}