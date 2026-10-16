namespace Api.Contracts.ContentPost;

public sealed class PublishNowResponse
{
    public ContentPostResponse Post { get; init; } = new();

    public string Outcome { get; init; } = string.Empty;

    public string? FailureReason { get; init; }

    public string? ExternalPostId { get; init; }
}
