using Api.Application.ContentEvaluation;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class ContentRepetitionDetectorTests
{
    private readonly ContentRepetitionDetector _detector = new();

    [Fact]
    public void Identical_content_is_repetitive()
    {
        var candidate = CreatePost("Stop guessing why your deploys fail and measure it instead.");

        var result = _detector.Evaluate(
            "Stop guessing why your deploys fail and measure it instead.",
            [candidate],
            0.75m);

        Assert.True(result.IsRepetitive);
        Assert.Equal(candidate.Id, result.SimilarToContentPostId);
        Assert.Equal(1m, result.Similarity);
        Assert.Equal(0.75m, result.Threshold);
    }

    [Fact]
    public void Unrelated_content_is_not_repetitive()
    {
        var candidate = CreatePost("Deploy dashboards measure the wrong thing entirely.");

        var result = _detector.Evaluate(
            "Stop guessing why your deploys fail and measure it instead.",
            [candidate],
            0.75m);

        Assert.False(result.IsRepetitive);
        Assert.True(result.Similarity < 0.75m);
    }

    [Fact]
    public void The_most_similar_candidate_is_reported()
    {
        var nearest = CreatePost("Stop guessing why your deploys fail and measure it instead.");
        var farther = CreatePost("Something completely unrelated about database indexes.");

        var result = _detector.Evaluate(
            "Stop guessing why your deploys fail and measure it instead.",
            [farther, nearest],
            0.75m);

        Assert.Equal(nearest.Id, result.SimilarToContentPostId);
        Assert.Equal(1m, result.Similarity);
    }

    [Fact]
    public void No_candidates_produces_no_similarity()
    {
        var result = _detector.Evaluate("Any content at all here.", [], 0.75m);

        Assert.False(result.IsRepetitive);
        Assert.Equal(0m, result.Similarity);
        Assert.Null(result.SimilarToContentPostId);
    }

    [Fact]
    public void Tokenization_ignores_case_punctuation_and_short_tokens()
    {
        var tokens = ContentRepetitionDetector.Tokenize("The AI, a Tool — for Devs!");

        Assert.Contains("the", tokens);
        Assert.Contains("tool", tokens);
        Assert.Contains("for", tokens);
        Assert.Contains("devs", tokens);
        Assert.DoesNotContain("a", tokens);
    }

    [Fact]
    public void Tokenization_of_blank_content_is_empty()
    {
        Assert.Empty(ContentRepetitionDetector.Tokenize("   "));
        Assert.Empty(ContentRepetitionDetector.Tokenize(null));
    }

    [Fact]
    public void A_blank_candidate_cannot_match()
    {
        var result = _detector.Evaluate(
            "Stop guessing why your deploys fail.",
            [CreatePost("   ")],
            0.75m);

        Assert.False(result.IsRepetitive);
        Assert.Equal(0m, result.Similarity);
    }

    [Fact]
    public void Similarity_threshold_is_configurable()
    {
        var content = "Stop guessing why your deploys fail and measure the cause instead.";
        var candidate = CreatePost(content);

        Assert.True(_detector.Evaluate(content, [candidate], 0.9m).IsRepetitive);
        Assert.False(_detector.Evaluate(content, [candidate], 1.1m).IsRepetitive);
    }

    private static ContentPost CreatePost(string content) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        ContentIdeaId = Guid.NewGuid(),
        Platform = ContentPlatform.LinkedIn,
        Status = ContentPostStatus.Draft,
        Content = content,
        Hook = "Hook",
        CallToAction = string.Empty,
        Hashtags = [],
        ContentType = ContentType.Insight,
        StorytellingStructure = "Hook → Insight",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
