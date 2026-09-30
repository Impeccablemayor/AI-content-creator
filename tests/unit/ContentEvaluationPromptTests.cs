using Api.Application.AI;
using Api.Application.AI.Prompts;
using Xunit;

namespace Api.Tests;

public sealed class ContentEvaluationPromptTests
{
    [Theory]
    [InlineData("generic motivational language")]
    [InlineData("fabricated personal experience")]
    [InlineData("Do not assume any claim is true")]
    [InlineData("unsupported statistics")]
    [InlineData("generic advice that could apply to anyone")]
    [InlineData("actually addresses the supplied target audience")]
    [InlineData("matches the supplied brand positioning")]
    [InlineData("matches the requested platform")]
    [InlineData("delivers the intended idea and the declared content type")]
    [InlineData("whether you personally agree")]
    [InlineData("Do not rewrite")]
    [InlineData("Return only JSON")]
    public void Evaluator_prompt_states_every_required_rule(string rule)
    {
        Assert.Contains(rule, ContentEvaluationSystemPrompt.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluator_schema_is_a_distinct_named_contract()
    {
        Assert.Equal("content_quality_evaluation", ContentEvaluationResponseSchema.Value.Name);
        Assert.NotEqual(ContentPostResponseSchema.Value.Name, ContentEvaluationResponseSchema.Value.Name);
        Assert.NotEqual(ContentIdeaResponseSchema.Value.Name, ContentEvaluationResponseSchema.Value.Name);
    }

    [Fact]
    public void Evaluator_schema_requires_all_ten_dimensions_plus_lists()
    {
        var schema = ContentEvaluationResponseSchema.Value.Schema;
        var required = schema
            .GetType().GetProperty("required")!
            .GetValue(schema) as string[] ?? [];

        Assert.Equal(
            [
                "strategicFit",
                "audienceFit",
                "hookStrength",
                "value",
                "clarity",
                "originality",
                "storytelling",
                "platformFit",
                "voiceMatch",
                "factualReliability",
                "strengths",
                "issues"
            ],
            required);
    }

    [Fact]
    public void Evaluator_schema_constrains_scores_to_the_documented_range()
    {
        var schema = ContentEvaluationResponseSchema.Value.Schema;
        var properties = schema.GetType().GetProperty("properties")!.GetValue(schema)!;
        var strategicFit = properties.GetType()
            .GetProperty("strategicFit")!
            .GetValue(properties)!;

        Assert.Equal("number", strategicFit.GetType().GetProperty("type")!.GetValue(strategicFit));
        Assert.Equal(0, strategicFit.GetType().GetProperty("minimum")!.GetValue(strategicFit));
        Assert.Equal(100, strategicFit.GetType().GetProperty("maximum")!.GetValue(strategicFit));
    }

    [Fact]
    public void Evaluator_schema_forbids_unexpected_properties()
    {
        var schema = ContentEvaluationResponseSchema.Value.Schema;

        Assert.Equal(
            false,
            schema.GetType().GetProperty("additionalProperties")!.GetValue(schema));
    }
}
