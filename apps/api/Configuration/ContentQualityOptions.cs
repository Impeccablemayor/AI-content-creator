namespace Api.Configuration;

public sealed class ContentQualityOptions
{
    public const string SectionName = "ContentQuality";

    public decimal PassThreshold { get; set; } = 70m;

    public decimal RepetitionSimilarityThreshold { get; set; } = 0.75m;
}
