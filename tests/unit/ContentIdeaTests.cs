using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentIdeas;
using Api.Application.AI.Models;
using Api.Contracts.ContentIdea;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public sealed class ContentIdeaTests
{
    [Fact]
    public void Overall_score_uses_application_weights_and_neutral_freshness()
    {
        var score = ContentIdeaScoring.Calculate(80m, 70m, 90m);

        Assert.Equal(76.50m, score);
    }

    [Fact]
    public void Ai_component_scores_are_clamped_to_supported_range()
    {
        var result = ContentIdeaGenerationValidator.ValidateAndNormalize(
            new ContentIdeaGenerationResult
            {
                Ideas =
                [
                    new GeneratedContentIdea
                    {
                        Title = "Title",
                        Topic = "Topic",
                        Angle = "Angle",
                        Hook = "Hook",
                        Reason = "Reason",
                        Scores = new GeneratedContentIdeaScores
                        {
                            Relevance = -4,
                            EngagementPotential = 130,
                            BrandFit = 42
                        }
                    }
                ]
            },
            1);

        var scores = Assert.IsType<GeneratedContentIdeaScores>(result[0].Scores);
        Assert.Equal(0m, scores.Relevance);
        Assert.Equal(100m, scores.EngagementPotential);
        Assert.Equal(42m, scores.BrandFit);
    }

    [Fact]
    public void Missing_required_ai_content_is_rejected()
    {
        var response = new ContentIdeaGenerationResult
        {
            Ideas =
            [
                new GeneratedContentIdea
                {
                    Title = " ",
                    Topic = "Topic",
                    Angle = "Angle",
                    Hook = "Hook",
                    Reason = "Reason",
                    Scores = new GeneratedContentIdeaScores()
                }
            ]
        };

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentIdeaGenerationValidator.ValidateAndNormalize(response, 1));
    }

    [Fact]
    public void Request_rejects_counts_outside_one_to_twenty()
    {
        var request = new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 21
        };

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
    }

    [Fact]
    public async Task Listing_and_approval_are_restricted_to_current_workspace()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var ownedIdea = CreateIdea(workspaceId, 75m);
        var otherIdea = CreateIdea(otherWorkspaceId, 95m);
        dbContext.ContentIdeas.AddRange(ownedIdea, otherIdea);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, workspaceId);
        var ideas = await service.GetAsync(null, null);
        var foreignApproval = await service.ApproveAsync(otherIdea.Id);
        var ownedApproval = await service.ApproveAsync(ownedIdea.Id);

        Assert.Single(ideas);
        Assert.Equal(ownedIdea.Id, ideas[0].Id);
        Assert.Null(foreignApproval);
        Assert.Equal(nameof(ContentIdeaStatus.Approved), ownedApproval!.Status);
        Assert.Equal(
            ContentIdeaStatus.Approved,
            (await dbContext.ContentIdeas.FindAsync(ownedIdea.Id))!.Status);
    }

    [Fact]
    public async Task Rejected_idea_cannot_be_approved_again()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = CreateIdea(workspaceId, 60m);
        dbContext.ContentIdeas.Add(idea);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, workspaceId);
        await service.RejectAsync(idea.Id);

        await Assert.ThrowsAsync<InvalidContentIdeaTransitionException>(
            () => service.ApproveAsync(idea.Id));
    }

    [Fact]
    public async Task Generation_persists_validated_ideas_for_current_workspace()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        dbContext.BrandProfiles.Add(new BrandProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Example brand",
            Industry = "Technology"
        });
        dbContext.AudienceProfiles.Add(new AudienceProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Description = "Software developers"
        });
        dbContext.ContentStrategies.Add(new ContentStrategy
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentPillars = "Engineering"
        });
        await dbContext.SaveChangesAsync();

        var aiProvider = new StubAiProvider("""
            {"ideas":[{"title":"A useful idea","topic":"Engineering",
            "angle":"Practical lessons","hook":"Start here","reason":"Relevant",
            "hashtags":["#engineering"],"scores":{"relevance":80,
            "engagementPotential":70,"brandFit":90}}]}
            """);
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var generated = await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1,
            TopicHint = "Testing"
        });

        Assert.Single(generated);
        Assert.Equal(76.50m, generated[0].OverallScore);
        Assert.Equal("#engineering", generated[0].SuggestedHashtags);
        Assert.Contains("Example brand", aiProvider.UserPrompt);
        Assert.Contains("Software developers", aiProvider.UserPrompt);
        Assert.Contains("Engineering", aiProvider.UserPrompt);
        Assert.Contains("LinkedIn", aiProvider.UserPrompt);
        Assert.Contains("Testing", aiProvider.UserPrompt);
        Assert.Contains("0-3 highly relevant hashtags", aiProvider.UserPrompt);
        Assert.Equal(workspaceId, (await dbContext.ContentIdeas.SingleAsync()).WorkspaceId);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ContentIdeaService CreateService(
        AppDbContext dbContext,
        Guid workspaceId,
        IAiProvider? aiProvider = null)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim("workspace_id", workspaceId.ToString())
                ], "test"))
            }
        };

        return new ContentIdeaService(
            dbContext,
            new CurrentUserService(accessor),
            aiProvider ?? new StubAiProvider("{}"),
            new ContentIdeaPromptBuilder());
    }

    private static ContentIdea CreateIdea(Guid workspaceId, decimal score) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = workspaceId,
        Status = ContentIdeaStatus.Suggested,
        Platform = ContentPlatform.LinkedIn,
        Title = "Title",
        Topic = "Topic",
        Angle = "Angle",
        Hook = "Hook",
        Reason = "Reason",
        SuggestedHashtags = "#topic",
        RelevanceScore = score,
        EngagementPotentialScore = score,
        BrandFitScore = score,
        OverallScore = score,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed class StubAiProvider(string response) : IAiProvider
    {
        public string UserPrompt { get; private set; } = string.Empty;

        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            UserPrompt = userPrompt;
            return Task.FromResult(response);
        }
    }
}