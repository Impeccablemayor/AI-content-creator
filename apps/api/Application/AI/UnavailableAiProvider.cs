using Api.Application.ContentIdeas;

namespace Api.Application.AI;

public sealed class UnavailableAiProvider : IAiProvider
{
    public Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        AiResponseSchema responseSchema,
        CancellationToken cancellationToken = default)
    {
        return Task.FromException<string>(
            new AiProviderUnavailableException());
    }
}
