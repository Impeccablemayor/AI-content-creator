using Api.Application.AI.Models;
using Api.Application.Learning;
using Xunit;

namespace Api.Tests;

public sealed class LearningInsightValidatorTests
{
    [Fact]
    public void A_recommendation_is_returned_trimmed()
    {
        var result = LearningInsightValidator.ValidateAndNormalize(
            new LearningInsightResult { Recommendation = "  Try one more Automation post.  " });

        Assert.Equal("Try one more Automation post.", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_recommendation_is_rejected(string? recommendation)
    {
        var failure = Assert.Throws<LearningInsightFailedException>(() =>
            LearningInsightValidator.ValidateAndNormalize(
                new LearningInsightResult { Recommendation = recommendation }));

        Assert.Contains("omitted the recommendation", failure.Message);
    }

    [Fact]
    public void An_absent_response_is_rejected()
    {
        var failure = Assert.Throws<LearningInsightFailedException>(() =>
            LearningInsightValidator.ValidateAndNormalize(null));

        Assert.Contains("empty", failure.Message);
    }

    [Fact]
    public void A_recommendation_at_the_length_limit_is_kept()
    {
        var atLimit = new string('a', LearningInsightValidator.MaxRecommendationLength);

        Assert.Equal(atLimit, LearningInsightValidator.ValidateAndNormalize(
            new LearningInsightResult { Recommendation = atLimit }));
    }

    /// <summary>
    /// A stored signal is meant to be read, so a recommendation that runs to
    /// several paragraphs is rejected rather than truncated into something that
    /// reads as a different claim from the one the model made.
    /// </summary>
    [Fact]
    public void A_recommendation_over_the_length_limit_is_rejected_rather_than_cut()
    {
        var tooLong = new string('a', LearningInsightValidator.MaxRecommendationLength + 1);

        var failure = Assert.Throws<LearningInsightFailedException>(() =>
            LearningInsightValidator.ValidateAndNormalize(
                new LearningInsightResult { Recommendation = tooLong }));

        Assert.Contains("over the", failure.Message);
    }
}
