namespace Api.Contracts.ContentPost;

public sealed class ContentEvaluationResponse
{
    public Guid ContentPostId { get; init; }

    public decimal OverallScore { get; init; }

    public string Recommendation { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public decimal PassThreshold { get; init; }

    public ContentEvaluationDimensionsResponse Dimensions { get; init; } = new();

    public IReadOnlyList<string> Strengths { get; init; } = [];

    public IReadOnlyList<string> Issues { get; init; } = [];

    public ContentRepetitionResponse Repetition { get; init; } = new();
}

public sealed class ContentEvaluationDimensionsResponse
{
    public decimal StrategicFit { get; init; }

    public decimal AudienceFit { get; init; }

    public decimal HookStrength { get; init; }

    public decimal Value { get; init; }

    public decimal Clarity { get; init; }

    public decimal Originality { get; init; }

    public decimal Storytelling { get; init; }

    public decimal PlatformFit { get; init; }

    public decimal VoiceMatch { get; init; }

    public decimal FactualReliability { get; init; }
}

public sealed class ContentRepetitionResponse
{
    public bool IsRepetitive { get; init; }

    public decimal Similarity { get; init; }

    public Guid? SimilarToContentPostId { get; init; }

    public decimal Threshold { get; init; }
}
