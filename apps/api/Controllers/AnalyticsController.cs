using Api.Application.Analytics;
using Api.Contracts.Analytics;
using Api.Contracts.Responses;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] ContentPlatform? platform,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? pillarId,
        CancellationToken cancellationToken)
    {
        var response = await _analyticsService.GetOverviewAsync(
            BuildQuery(platform, from, to, pillarId),
            cancellationToken);

        return Ok(new ApiResponse<AnalyticsOverviewResponse>
        {
            Data = response,
            Message = "Analytics overview retrieved successfully."
        });
    }

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts(
        [FromQuery] ContentPlatform? platform,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? pillarId,
        CancellationToken cancellationToken)
    {
        var response = (await _analyticsService.GetPostsAsync(
            BuildQuery(platform, from, to, pillarId),
            cancellationToken)).ToList();

        return Ok(new ApiResponse<List<PostAnalyticsResponse>>
        {
            Data = response,
            Message = "Post analytics retrieved successfully."
        });
    }

    /// <summary>
    /// The API specification names this route parameter
    /// <c>{publicationId}</c>. There is no publication entity — publishing
    /// records its outcome on the content post — so the route is keyed by
    /// content post id.
    /// </summary>
    [HttpGet("posts/{id:guid}")]
    public async Task<IActionResult> GetPost(Guid id, CancellationToken cancellationToken)
    {
        var post = await _analyticsService.GetPostAsync(id, cancellationToken);
        if (post is null)
        {
            return NotFound(CreateError(
                "ANALYTICS_POST_NOT_FOUND",
                "No published content post with analytics was found."));
        }

        return Ok(new ApiResponse<PostAnalyticsResponse>
        {
            Data = post,
            Message = "Post analytics retrieved successfully."
        });
    }

    private static AnalyticsQuery BuildQuery(
        ContentPlatform? platform,
        DateTime? from,
        DateTime? to,
        Guid? pillarId)
    {
        if (from is not null && to is not null && from > to)
        {
            throw new InvalidAnalyticsPeriodException();
        }

        return new AnalyticsQuery(platform, from, to, pillarId);
    }

    private static ApiErrorResponse CreateError(string code, string message) => new()
    {
        Error = new ApiError
        {
            Code = code,
            Message = message
        },
        TraceId = System.Diagnostics.Activity.Current?.Id ?? string.Empty
    };
}
