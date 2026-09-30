using System.Text.Json;
using Api.Application.AI;
using Api.Application.AI.Models;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Contracts.ContentIdea;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Application.ContentIdeas;

public sealed class ContentIdeaService : IContentIdeaService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;
    private readonly IAiProvider _aiProvider;
    private readonly ContentIdeaPromptBuilder _promptBuilder;

    public ContentIdeaService(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        IAiProvider aiProvider,
        ContentIdeaPromptBuilder promptBuilder)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _aiProvider = aiProvider;
        _promptBuilder = promptBuilder;
    }

    public async Task<IReadOnlyList<ContentIdeaResponse>> GetAsync(
        ContentIdeaStatus? status,
        ContentPlatform? platform,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ContentIdeas
            .AsNoTracking()
            .Where(idea => idea.WorkspaceId == _currentUser.WorkspaceId);

        if (status.HasValue)
        {
            query = query.Where(idea => idea.Status == status.Value);
        }

        if (platform.HasValue)
        {
            query = query.Where(idea => idea.Platform == platform.Value);
        }

        var ideas = await query
            .OrderByDescending(idea => idea.OverallScore)
            .ThenByDescending(idea => idea.CreatedAt)
            .ToListAsync(cancellationToken);

        return ideas.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ContentIdeaResponse>> GenerateAsync(
        GenerateContentIdeasRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Count is < 1 or > 20)
        {
            throw new InvalidContentIdeaRequestException(
                "Count must be between 1 and 20.");
        }

        if (!Enum.IsDefined(request.Platform))
        {
            throw new InvalidContentIdeaRequestException(
                "The requested platform is not supported.");
        }

        var workspaceId = _currentUser.WorkspaceId;
        var brand = await _dbContext.BrandProfiles.AsNoTracking()
            .SingleOrDefaultAsync(
                profile => profile.WorkspaceId == workspaceId,
                cancellationToken);
        var audience = await _dbContext.AudienceProfiles.AsNoTracking()
            .SingleOrDefaultAsync(
                profile => profile.WorkspaceId == workspaceId,
                cancellationToken);
        var strategy = await _dbContext.ContentStrategies.AsNoTracking()
            .SingleOrDefaultAsync(
                profile => profile.WorkspaceId == workspaceId,
                cancellationToken);

        if (brand is null || audience is null || strategy is null)
        {
            throw new ContentIdeaContextIncompleteException();
        }

        var prompt = _promptBuilder.Build(
            brand,
            audience,
            strategy,
            request.Platform,
            request.Count,
            request.TopicHint,
            DateTimeOffset.UtcNow);

        var generatedJson = await _aiProvider.GenerateAsync(
            ContentIdeaSystemPrompt.Value,
            prompt,
            ContentIdeaResponseSchema.Value,
            cancellationToken);

        ContentIdeaGenerationResult? generated;
        try
        {
            generated = JsonSerializer.Deserialize<ContentIdeaGenerationResult>(
                generatedJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw new InvalidAiResponseException(
                "The AI response was not valid structured JSON.");
        }

        var validatedIdeas = ContentIdeaGenerationValidator
            .ValidateAndNormalize(generated, request.Count);
        var now = DateTime.UtcNow;
        var entities = validatedIdeas.Select(idea => new ContentIdea
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Status = ContentIdeaStatus.Suggested,
            Platform = request.Platform,
            Title = idea.Title!,
            Topic = idea.Topic!,
            Angle = idea.Angle!,
            Hook = idea.Hook!,
            Reason = idea.Reason!,
            SuggestedHashtags = string.Join(" ", idea.Hashtags!),
            RelevanceScore = idea.Scores!.Relevance,
            EngagementPotentialScore = idea.Scores.EngagementPotential,
            BrandFitScore = idea.Scores.BrandFit,
            OverallScore = ContentIdeaScoring.Calculate(
                idea.Scores.Relevance,
                idea.Scores.EngagementPotential,
                idea.Scores.BrandFit),
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        _dbContext.ContentIdeas.AddRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public Task<ContentIdeaResponse?> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(id, ContentIdeaStatus.Approved, cancellationToken);

    public Task<ContentIdeaResponse?> RejectAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(id, ContentIdeaStatus.Rejected, cancellationToken);

    private async Task<ContentIdeaResponse?> UpdateStatusAsync(
        Guid id,
        ContentIdeaStatus newStatus,
        CancellationToken cancellationToken)
    {
        var idea = await _dbContext.ContentIdeas.FirstOrDefaultAsync(
            item => item.Id == id &&
                item.WorkspaceId == _currentUser.WorkspaceId,
            cancellationToken);

        if (idea is null)
        {
            return null;
        }

        if (idea.Status != ContentIdeaStatus.Suggested)
        {
            throw new InvalidContentIdeaTransitionException();
        }

        idea.Status = newStatus;
        idea.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(idea);
    }

    private static ContentIdeaResponse Map(ContentIdea idea) => new()
    {
        Id = idea.Id,
        Status = idea.Status.ToString(),
        Platform = idea.Platform.ToString(),
        Title = idea.Title,
        Topic = idea.Topic,
        Angle = idea.Angle,
        Hook = idea.Hook,
        Reason = idea.Reason,
        SuggestedHashtags = idea.SuggestedHashtags,
        RelevanceScore = idea.RelevanceScore,
        EngagementPotentialScore = idea.EngagementPotentialScore,
        BrandFitScore = idea.BrandFitScore,
        OverallScore = idea.OverallScore,
        SuggestedPublishAt = idea.SuggestedPublishAt,
        CreatedAt = idea.CreatedAt
    };
}