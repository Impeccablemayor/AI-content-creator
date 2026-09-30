namespace Api.Application.AI.Models;

public sealed class ContentEvaluationResult
{
    public decimal? StrategicFit { get; set; }

    public decimal? AudienceFit { get; set; }

    public decimal? HookStrength { get; set; }

    public decimal? Value { get; set; }

    public decimal? Clarity { get; set; }

    public decimal? Originality { get; set; }

    public decimal? Storytelling { get; set; }

    public decimal? PlatformFit { get; set; }

    public decimal? VoiceMatch { get; set; }

    public decimal? FactualReliability { get; set; }

    public List<string>? Strengths { get; set; }

    public List<string>? Issues { get; set; }
}
