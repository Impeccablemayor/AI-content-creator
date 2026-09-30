namespace Api.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "Unavailable";

    public string Model { get; set; } = "gpt-4o-mini";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 45;
}