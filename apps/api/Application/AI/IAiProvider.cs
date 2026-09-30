namespace Api.Application.AI;

public interface IAiProvider
{
    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        AiResponseSchema responseSchema,
        CancellationToken cancellationToken = default);
}
