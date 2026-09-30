using Api.Application.AI.Models;
using Api.Application.ContentEvaluation;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class ContentEvaluationTests
{
    [Fact]
    public void Valid_evaluation_response_parses_every_dimension()
    {
        var validated = ContentEvaluationValidator.ValidateAndNormalize(CreateResult());

        Assert.Equal(90m, validated.StrategicFit);
        Assert.Equal(86m, validated.AudienceFit);
        Assert.Equal(82m, validated.HookStrength);
        Assert.Equal(91m, validated.Value);
        Assert.Equal(94m, validated.Clarity);
        Assert.Equal(80m, validated.Originality);
        Assert.Equal(84m, validated.Storytelling);
        Assert.Equal(89m, validated.PlatformFit);
        Assert.Equal(93m, validated.VoiceMatch);
        Assert.Equal(96m, validated.FactualReliability);
        Assert.Equal(["Specific to the audience"], validated.Strengths);
        Assert.Equal(["CTA is weak"], validated.Issues);
    }

    [Fact]
    public void Null_evaluation_response_fails()
    {
        Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(null));
    }

    [Theory]
    [InlineData("StrategicFit")]
    [InlineData("AudienceFit")]
    [InlineData("HookStrength")]
    [InlineData("Value")]
    [InlineData("Clarity")]
    [InlineData("Originality")]
    [InlineData("Storytelling")]
    [InlineData("PlatformFit")]
    [InlineData("VoiceMatch")]
    [InlineData("FactualReliability")]
    public void Missing_required_dimension_fails(string missing)
    {
        var result = CreateResult();
        typeof(ContentEvaluationResult)
            .GetProperty(missing)!
            .SetValue(result, null);

        var exception = Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(result));

        Assert.Contains(
            missing.Substring(0, 1).ToLowerInvariant() + missing.Substring(1),
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("omitted", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Out_of_range_dimension_score_fails(int score)
    {
        var result = CreateResult();
        result.Value = score;

        var exception = Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(result));

        Assert.Contains("out-of-range value", exception.Message);
    }

    [Fact]
    public void Missing_lists_fail()
    {
        var withoutStrengths = CreateResult();
        withoutStrengths.Strengths = null;
        var withoutIssues = CreateResult();
        withoutIssues.Issues = null;

        Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(withoutStrengths));
        Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(withoutIssues));
    }

    [Fact]
    public void Too_many_list_entries_fail()
    {
        var result = CreateResult();
        result.Issues = ["a", "b", "c", "d", "e", "f"];

        var exception = Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(result));

        Assert.Contains("too many issues", exception.Message);
    }

    [Fact]
    public void Blank_list_entry_fails()
    {
        var result = CreateResult();
        result.Strengths = ["  "];

        Assert.Throws<ContentEvaluationFailedException>(() =>
            ContentEvaluationValidator.ValidateAndNormalize(result));
    }

    [Fact]
    public void List_entries_are_trimmed()
    {
        var result = CreateResult();
        result.Strengths = ["  Specific  "];

        var validated = ContentEvaluationValidator.ValidateAndNormalize(result);

        Assert.Equal(["Specific"], validated.Strengths);
    }

    [Theory]
    [InlineData(95.0, 95.00, "Accept", true)]
    [InlineData(85.0, 85.00, "MinorEdit", true)]
    [InlineData(75.0, 75.00, "ConsiderImprovement", true)]
    [InlineData(65.0, 65.00, "Revise", false)]
    [InlineData(50.0, 50.00, "Rewrite", false)]
    public void Recommendation_and_pass_follow_the_documented_bands(
        double mean,
        double expectedScore,
        string expectedRecommendation,
        bool expectedPass)
    {
        var result = ScoreAll((decimal)mean);

        var overall = ContentEvaluationScoring.CalculateOverallScore(result);

        Assert.Equal((decimal)expectedScore, overall);
        Assert.Equal(expectedRecommendation, ContentEvaluationScoring.Recommend(overall).ToString());
        Assert.Equal(expectedPass, ContentEvaluationScoring.Passes(overall, 70m));
    }

    [Fact]
    public void Overall_score_is_the_mean_of_the_ten_dimensions()
    {
        var result = ScoreAll(80m);
        result.FactualReliability = 60m;

        var overall = ContentEvaluationScoring.CalculateOverallScore(result);

        Assert.Equal(78m, overall);
    }

    [Fact]
    public void Pass_threshold_is_configurable()
    {
        Assert.True(ContentEvaluationScoring.Passes(65m, 60m));
        Assert.False(ContentEvaluationScoring.Passes(65m, 70m));
    }

    [Fact]
    public void Overall_score_is_rounded_to_two_decimal_places()
    {
        var result = ScoreAll(0m);
        result.StrategicFit = 1m;

        var overall = ContentEvaluationScoring.CalculateOverallScore(result);

        Assert.Equal(0.1m, overall);
    }

    private static ContentEvaluationResult CreateResult() => new()
    {
        StrategicFit = 90m,
        AudienceFit = 86m,
        HookStrength = 82m,
        Value = 91m,
        Clarity = 94m,
        Originality = 80m,
        Storytelling = 84m,
        PlatformFit = 89m,
        VoiceMatch = 93m,
        FactualReliability = 96m,
        Strengths = ["Specific to the audience"],
        Issues = ["CTA is weak"]
    };

    private static ContentEvaluationResult ScoreAll(decimal score) => new()
    {
        StrategicFit = score,
        AudienceFit = score,
        HookStrength = score,
        Value = score,
        Clarity = score,
        Originality = score,
        Storytelling = score,
        PlatformFit = score,
        VoiceMatch = score,
        FactualReliability = score,
        Strengths = [],
        Issues = []
    };
}
