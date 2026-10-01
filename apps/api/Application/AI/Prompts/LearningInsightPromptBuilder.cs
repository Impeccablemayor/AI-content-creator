using System.Globalization;
using System.Text.Json;
using Api.Application.Learning;
using Api.Domain.Entities;

namespace Api.Application.AI.Prompts;

/// <summary>
/// Builds the single user message sent for one learning signal.
/// </summary>
public sealed class LearningInsightPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Build(
        PerformanceFinding finding,
        DateTime from,
        DateTime to,
        int postsInWindow,
        int measuredPosts,
        BrandProfile? brand,
        AudienceProfile? audience,
        ContentStrategy? strategy)
    {
        var context = new
        {
            measuredEvidence = new
            {
                // The observation is included as the exact text already stored on
                // the signal, so the model reads the same numbers a user will.
                // It is context, not a field to fill in.
                observation = finding.Observation,
                dimension = finding.Dimension.ToString(),
                strongest = Describe(finding.Best),
                weakest = Describe(finding.Worst),
                allGroups = finding.Groups.Select(Describe).ToArray(),
                measuredPosts,
                evidenceCount = finding.EvidenceCount,
                evidenceStrength = finding.Confidence
            },
            window = new
            {
                from = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                to = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                publishedPostsInWindow = postsInWindow
            },
            // Only supplied when the workspace has actually defined it. A missing
            // section means unknown, which the system prompt tells the model not
            // to read as empty.
            context = new
            {
                brand = brand is null ? null : new
                {
                    brand.Name,
                    brand.Description,
                    brand.Industry,
                    brand.ToneOfVoice,
                    brand.ContentGoals
                },
                audience = audience is null ? null : new
                {
                    audience.Description,
                    audience.Demographics,
                    audience.Interests,
                    audience.PainPoints,
                    audience.Goals
                },
                contentStrategy = strategy is null ? null : new
                {
                    strategy.ContentPillars,
                    strategy.ContentObjectives,
                    strategy.PreferredFormats
                }
            },
            note = "Recommendation only. Do not restate the measured evidence, do not explain the data, and do not claim a cause. The weakest group is compared by engagement rate; posts in it were not necessarily worse in every other respect."
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }

    private static object Describe(LearningGroup group) => new
    {
        group.Label,
        posts = group.Posts,
        impressions = group.Impressions,
        engagementRatePercent = group.EngagementRate
    };
}
