using Api.Application.Authentication;
using Api.Contracts.ContentStrategy;
using Api.Contracts.Responses;
using Api.Domain.Entities;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/content-strategy")]
public sealed class ContentStrategiesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;

    public ContentStrategiesController(
        AppDbContext dbContext,
        CurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var strategy = await _dbContext.ContentStrategies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WorkspaceId == _currentUser.WorkspaceId,
                cancellationToken);

        if (strategy is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "CONTENT_STRATEGY_NOT_FOUND",
                    Message = "Content strategy has not been configured."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new ApiResponse<ContentStrategyResponse>
        {
            Data = Map(strategy),
            Message = "Content strategy retrieved successfully."
        });
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        UpdateContentStrategyRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PostsPerWeek < 1 ||
            request.PostsPerWeek > 50)
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "INVALID_POSTING_FREQUENCY",
                    Message = "Posts per week must be between 1 and 50."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var strategy = await _dbContext.ContentStrategies
            .FirstOrDefaultAsync(
                x => x.WorkspaceId == _currentUser.WorkspaceId,
                cancellationToken);

        if (strategy is null)
        {
            strategy = new ContentStrategy
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _currentUser.WorkspaceId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ContentStrategies.Add(strategy);
        }

        strategy.PostsPerWeek = request.PostsPerWeek;
        strategy.ContentPillars = request.ContentPillars.Trim();
        strategy.ContentObjectives = request.ContentObjectives.Trim();
        strategy.PreferredFormats = request.PreferredFormats.Trim();
        strategy.StorytellingStyle = request.StorytellingStyle.Trim();
        strategy.HashtagStrategy = request.HashtagStrategy.Trim();
        strategy.TopicsToPrioritize = request.TopicsToPrioritize.Trim();
        strategy.TopicsToAvoid = request.TopicsToAvoid.Trim();
        strategy.PreferredPostingTimes =
            request.PreferredPostingTimes.Trim();

        strategy.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<ContentStrategyResponse>
        {
            Data = Map(strategy),
            Message = "Content strategy saved successfully."
        });
    }

    private static ContentStrategyResponse Map(
        ContentStrategy strategy)
    {
        return new ContentStrategyResponse
        {
            Id = strategy.Id,
            WorkspaceId = strategy.WorkspaceId,
            PostsPerWeek = strategy.PostsPerWeek,
            ContentPillars = strategy.ContentPillars,
            ContentObjectives = strategy.ContentObjectives,
            PreferredFormats = strategy.PreferredFormats,
            StorytellingStyle = strategy.StorytellingStyle,
            HashtagStrategy = strategy.HashtagStrategy,
            TopicsToPrioritize = strategy.TopicsToPrioritize,
            TopicsToAvoid = strategy.TopicsToAvoid,
            PreferredPostingTimes = strategy.PreferredPostingTimes
        };
    }
}