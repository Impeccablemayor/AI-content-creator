namespace Api.Domain.Enums;

/// <summary>
/// The kind of significant AI operation that a run row records.
/// </summary>
/// <remarks>
/// The set is the eleven run types listed in the database schema (section 19.1).
/// Only the four that the code can actually produce today have a writer:
/// <see cref="IdeaGeneration"/>, <see cref="ContentGeneration"/>,
/// <see cref="QualityEvaluation"/>, and <see cref="PerformanceAnalysis"/>.
/// The rest are declared because the schema defines them, not because something
/// records them yet; declaring a member changes no database structure, so the
/// remaining types can be wired later without a migration.
/// </remarks>
public enum AIRunType
{
    /// <summary>Research gathering. No caller yet.</summary>
    Research = 1,

    /// <summary>Generating content ideas for a platform.</summary>
    IdeaGeneration = 2,

    /// <summary>Scoring content opportunities. No caller yet.</summary>
    OpportunityScoring = 3,

    /// <summary>Producing or revising the content strategy. No caller yet.</summary>
    Strategy = 4,

    /// <summary>Generating or regenerating a content post.</summary>
    ContentGeneration = 5,

    /// <summary>Adapting a post to a platform. No caller yet.</summary>
    PlatformOptimization = 6,

    /// <summary>Scoring an already-written post for quality.</summary>
    QualityEvaluation = 7,

    /// <summary>Checking claims for accuracy. No caller yet.</summary>
    FactVerification = 8,

    /// <summary>
    /// Interpreting measured performance as a recommendation.
    /// </summary>
    /// <remarks>
    /// Named for the analysis the call serves rather than the signal it
    /// produces. <see cref="Learning"/> stays unwired because deriving the
    /// signal is deterministic — the measurement is compared and the observation
    /// written by code — so the only model work in the learning loop is
    /// interpreting an analysis that has already happened.
    /// </remarks>
    PerformanceAnalysis = 9,

    /// <summary>Deriving learning signals. No caller yet.</summary>
    Learning = 10,

    /// <summary>Producing an embedding vector. No caller yet.</summary>
    Embedding = 11
}

/// <summary>
/// Converts <see cref="AIRunType"/> to and from the strings the schema lists.
/// </summary>
/// <remarks>
/// Every other enum in the model is stored as its .NET name through
/// <c>HasConversion&lt;string&gt;()</c>, which works only for single-word
/// members. The schema writes these run types in SCREAMING_SNAKE_CASE
/// (<c>IDEA_GENERATION</c>), so the mapping is spelled out instead of derived.
/// Deriving it by uppercasing the member name would produce
/// <c>IDEAGENERATION</c> and silently not match the schema.
/// </remarks>
public static class AIRunTypeStorage
{
    private static readonly Dictionary<AIRunType, string> Values = new()
    {
        [AIRunType.Research] = "RESEARCH",
        [AIRunType.IdeaGeneration] = "IDEA_GENERATION",
        [AIRunType.OpportunityScoring] = "OPPORTUNITY_SCORING",
        [AIRunType.Strategy] = "STRATEGY",
        [AIRunType.ContentGeneration] = "CONTENT_GENERATION",
        [AIRunType.PlatformOptimization] = "PLATFORM_OPTIMIZATION",
        [AIRunType.QualityEvaluation] = "QUALITY_EVALUATION",
        [AIRunType.FactVerification] = "FACT_VERIFICATION",
        [AIRunType.PerformanceAnalysis] = "PERFORMANCE_ANALYSIS",
        [AIRunType.Learning] = "LEARNING",
        [AIRunType.Embedding] = "EMBEDDING"
    };

    public static string ToStorage(AIRunType value) => Values[value];

    public static AIRunType FromStorage(string value) =>
        Values.FirstOrDefault(pair =>
            string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase)).Key;
}
