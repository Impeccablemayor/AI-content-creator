namespace Api.Application.AI.Models;

public sealed class ContentPostGenerationResult
{
    public string? Platform { get; init; }

    public string? Content { get; init; }

    public string? Hook { get; init; }

    public string? CallToAction { get; init; }

    public List<string?>? Hashtags { get; init; }

    public string? ContentType { get; init; }

    public string? StorytellingStructure { get; init; }
}