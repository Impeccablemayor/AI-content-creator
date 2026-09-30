using System.Diagnostics;
using System.Text.Json;
using Api.Application.AI;
using Api.Application.AI.Models;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentIdeas;
using Api.Application.ContentPosts;
using Api.Contracts.ContentPost;
using Api.Configuration;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Application.Services;

public sealed class ContentPostService : IContentPostService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;
    private readonly IAiProvider _aiProvider;
    private readonly ContentPostPromptBuilder _promptBuilder;
    private readonly ILogger<ContentPostService> _logger;
    private readonly AiOptions _aiOptions;

    public ContentPostService(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        IAiProvider aiProvider,
        ContentPostPromptBuilder promptBuilder,
        ILogger<ContentPostService> logger,
        IOptions<AiOptions> aiOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _aiProvider = aiProvider;
        _promptBuilder = promptBuilder;
        _logger = logger;
        _aiOptions = aiOptions.Value;
    }

    public async Task<ContentPostResponse?> GenerateFromIdeaAsync(
        Guid contentIdeaId,
        GenerateContentPostRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Platform) ||
            request.Platform is not (ContentPlatform.LinkedIn or ContentPlatform.X))
        {
            throw new InvalidContentPostRequestException(
                "Only LinkedIn and X posts can be generated.");
        }

        var workspaceId = _currentUser.WorkspaceId;
        var idea = await _dbContext.ContentIdeas.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == contentIdeaId && item.WorkspaceId == workspaceId,
                cancellationToken);
        if (idea is null)
        {
            return null;
        }

        if (idea.Status != ContentIdeaStatus.Approved)
        {
            throw new ContentIdeaInvalidStatusException();
        }

        var existingPost = await FindExistingPostAsync(
            workspaceId,
            contentIdeaId,
            request.Platform,
            cancellationToken);
        if (existingPost is not null)
        {
            return Map(existingPost);
        }

        var validated = await GenerateWithProviderAsync(
            idea,
            request.Platform,
            "generation",
            cancellationToken);

        var now = DateTime.UtcNow;
        var post = CreatePost(
            workspaceId,
            idea.Id,
            request.Platform,
            validated,
            now);

        _dbContext.ContentPosts.Add(post);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicatePlatformPost(exception))
        {
            var duplicate = await FindExistingPostAsync(
                workspaceId,
                contentIdeaId,
                request.Platform,
                cancellationToken);
            if (duplicate is not null)
            {
                return Map(duplicate);
            }

            throw;
        }

        return Map(post);
    }

    public async Task<IReadOnlyList<ContentPostResponse>> GetAsync(
        ContentPostStatus? status,
        ContentPlatform? platform,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var query = _dbContext.ContentPosts.AsNoTracking()
            .Where(post => post.WorkspaceId == workspaceId);

        if (status is not null)
        {
            query = query.Where(post => post.Status == status);
        }

        if (platform is not null)
        {
            query = query.Where(post => post.Platform == platform);
        }

        var posts = await query
            .OrderByDescending(post => post.CreatedAt)
            .ToListAsync(cancellationToken);

        return posts.Select(Map).ToList();
    }

    public async Task<ContentPostResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var post = await _dbContext.ContentPosts.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.WorkspaceId == workspaceId,
                cancellationToken);

        return post is null ? null : Map(post);
    }

    public async Task<ContentPostResponse?> UpdateAsync(
        Guid id,
        UpdateContentPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = await FindTrackedPostAsync(id, cancellationToken);
        if (post is null)
        {
            return null;
        }

        if (!ContentPostStatusTransitions.CanEdit(post.Status))
        {
            throw new InvalidContentPostTransitionException(
                $"Only draft content posts can be edited, but this post is {post.Status}.");
        }

        var validated = ContentPostGenerationValidator.ValidateEdited(
            new ContentPostGenerationResult
            {
                Platform = post.Platform.ToString(),
                Content = request.Content,
                Hook = request.Hook,
                CallToAction = request.CallToAction,
                Hashtags = request.Hashtags?.Select(hashtag => (string?)hashtag).ToList(),
                ContentType = request.ContentType.ToString(),
                StorytellingStructure = request.StorytellingStructure
            },
            post.Platform);

        Apply(post, validated, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(post);
    }

    public async Task<ContentPostResponse?> RegenerateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var post = await FindTrackedPostAsync(id, cancellationToken);
        if (post is null)
        {
            return null;
        }

        if (!ContentPostStatusTransitions.CanRegenerate(post.Status))
        {
            throw new InvalidContentPostTransitionException(
                $"Only draft or rejected content posts can be regenerated, but this post is {post.Status}.");
        }

        var workspaceId = _currentUser.WorkspaceId;
        var idea = await _dbContext.ContentIdeas.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == post.ContentIdeaId && item.WorkspaceId == workspaceId,
                cancellationToken);
        if (idea is null)
        {
            return null;
        }

        var validated = await GenerateWithProviderAsync(
            idea,
            post.Platform,
            "regeneration",
            cancellationToken);

        Apply(post, validated, DateTime.UtcNow);
        post.Status = ContentPostStatus.Draft;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(post);
    }

    public Task<ContentPostResponse?> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            ContentPostStatusTransitions.CanApprove,
            ContentPostStatus.Approved,
            "Only draft content posts can be approved.",
            cancellationToken);

    public Task<ContentPostResponse?> RejectAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            ContentPostStatusTransitions.CanReject,
            ContentPostStatus.Rejected,
            "Only draft content posts can be rejected.",
            cancellationToken);

    private async Task<ContentPostResponse?> TransitionAsync(
        Guid id,
        Func<ContentPostStatus, bool> isAllowed,
        ContentPostStatus target,
        string rejectionMessage,
        CancellationToken cancellationToken)
    {
        var post = await FindTrackedPostAsync(id, cancellationToken);
        if (post is null)
        {
            return null;
        }

        if (!isAllowed(post.Status))
        {
            throw new InvalidContentPostTransitionException(
                $"{rejectionMessage} This post is {post.Status}.");
        }

        post.Status = target;
        post.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(post);
    }

    private Task<ContentPost?> FindTrackedPostAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workspaceId = _currentUser.WorkspaceId;
        return _dbContext.ContentPosts
            .SingleOrDefaultAsync(
                post => post.Id == id && post.WorkspaceId == workspaceId,
                cancellationToken);
    }

    private async Task<ContentPostGenerationResult> GenerateWithProviderAsync(
        ContentIdea idea,
        ContentPlatform platform,
        string operation,
        CancellationToken cancellationToken)
    {
        var (brand, audience, strategy) = await LoadGenerationContextAsync(
            idea.WorkspaceId,
            cancellationToken);

        var startedAt = Stopwatch.GetTimestamp();
        _logger.LogInformation(
            "Content post {Operation} started for idea {ContentIdeaId}, platform {Platform}, provider {Provider}, model {Model}.",
            operation,
            idea.Id,
            platform,
            _aiOptions.Provider,
            _aiOptions.Model);

        try
        {
            var prompt = _promptBuilder.Build(
                brand,
                audience,
                strategy,
                idea,
                platform,
                DateTimeOffset.UtcNow);
            var generatedJson = await _aiProvider.GenerateAsync(
                ContentPostSystemPrompt.Value,
                prompt,
                ContentPostResponseSchema.Value,
                cancellationToken);

            ContentPostGenerationResult? generated;
            try
            {
                generated = JsonSerializer.Deserialize<ContentPostGenerationResult>(
                    generatedJson,
                    JsonOptions);
            }
            catch (JsonException)
            {
                throw new InvalidAiResponseException(
                    "The AI response was not valid structured JSON.");
            }

            var validated = ContentPostGenerationValidator.ValidateAndNormalize(
                generated,
                platform);

            _logger.LogInformation(
                "Content post {Operation} succeeded for idea {ContentIdeaId}, platform {Platform}, provider {Provider}, model {Model} in {DurationMilliseconds} ms.",
                operation,
                idea.Id,
                platform,
                _aiOptions.Provider,
                _aiOptions.Model,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return validated;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Content post {Operation} failed for idea {ContentIdeaId}, platform {Platform}, provider {Provider}, model {Model} after {DurationMilliseconds} ms with {FailureType}.",
                operation,
                idea.Id,
                platform,
                _aiOptions.Provider,
                _aiOptions.Model,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                exception.GetType().Name);
            throw;
        }
    }

    private async Task<(BrandProfile Brand, AudienceProfile Audience, ContentStrategy Strategy)>
        LoadGenerationContextAsync(
            Guid workspaceId,
            CancellationToken cancellationToken)
    {
        var brand = await _dbContext.BrandProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.WorkspaceId == workspaceId, cancellationToken);
        var audience = await _dbContext.AudienceProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.WorkspaceId == workspaceId, cancellationToken);
        var strategy = await _dbContext.ContentStrategies.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.WorkspaceId == workspaceId, cancellationToken);

        if (brand is null || audience is null || strategy is null)
        {
            throw new ContentIdeaContextIncompleteException();
        }

        return (brand, audience, strategy);
    }
    private static ContentPost CreatePost(
        Guid workspaceId,
        Guid contentIdeaId,
        ContentPlatform platform,
        ContentPostGenerationResult validated,
        DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = contentIdeaId,
            Platform = platform,
            Status = ContentPostStatus.Draft,
            Content = validated.Content!,
            Hook = validated.Hook!,
            CallToAction = validated.CallToAction!,
            Hashtags = validated.Hashtags!.Select(hashtag => hashtag!).ToList(),
            ContentType = Enum.Parse<ContentType>(validated.ContentType!, ignoreCase: true),
            StorytellingStructure = validated.StorytellingStructure!,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static void Apply(
        ContentPost post,
        ContentPostGenerationResult validated,
        DateTime now)
    {
        post.Content = validated.Content!;
        post.Hook = validated.Hook!;
        post.CallToAction = validated.CallToAction!;
        post.Hashtags = validated.Hashtags!.Select(hashtag => hashtag!).ToList();
        post.ContentType = Enum.Parse<ContentType>(validated.ContentType!, ignoreCase: true);
        post.StorytellingStructure = validated.StorytellingStructure!;
        post.UpdatedAt = now;
    }

    private Task<ContentPost?> FindExistingPostAsync(
        Guid workspaceId,
        Guid contentIdeaId,
        ContentPlatform platform,
        CancellationToken cancellationToken) =>
        _dbContext.ContentPosts.AsNoTracking()
            .SingleOrDefaultAsync(
                post => post.WorkspaceId == workspaceId &&
                    post.ContentIdeaId == contentIdeaId &&
                    post.Platform == platform,
                cancellationToken);

    private static bool IsDuplicatePlatformPost(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "IX_ContentPosts_ContentIdeaId_Platform")
            {
                return true;
            }
        }

        return false;
    }

    private static ContentPostResponse Map(ContentPost post) => new()
    {
        Id = post.Id,
        ContentIdeaId = post.ContentIdeaId,
        Platform = post.Platform.ToString(),
        Status = post.Status.ToString(),
        Content = post.Content,
        Hook = post.Hook,
        CallToAction = post.CallToAction,
        Hashtags = post.Hashtags,
        ContentType = post.ContentType.ToString(),
        StorytellingStructure = post.StorytellingStructure,
        CreatedAt = post.CreatedAt,
        UpdatedAt = post.UpdatedAt
    };
}
