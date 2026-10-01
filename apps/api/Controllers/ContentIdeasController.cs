using Api.Application.ContentIdeas;
using Api.Application.ContentPosts;
using Api.Contracts.ContentIdea;
using Api.Contracts.ContentPost;
using Api.Contracts.Responses;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/content-ideas")]
public sealed class ContentIdeasController : ControllerBase
{
    private readonly IContentIdeaService _contentIdeaService;
    private readonly IContentPostService _contentPostService;

    public ContentIdeasController(
        IContentIdeaService contentIdeaService,
        IContentPostService contentPostService)
    {
        _contentIdeaService = contentIdeaService;
        _contentPostService = contentPostService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] ContentIdeaStatus? status,
        [FromQuery] ContentPlatform? platform,
        [FromQuery] Guid? pillarId,
        CancellationToken cancellationToken)
    {
        var response = (await _contentIdeaService.GetAsync(
            status,
            platform,
            pillarId,
            cancellationToken)).ToList();

        return Ok(new ApiResponse<List<ContentIdeaResponse>>
        {
            Data = response,
            Message = "Content ideas retrieved successfully."
        });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateContentIdeasRequest request,
        CancellationToken cancellationToken)
    {
        var ideas = await _contentIdeaService.GenerateAsync(
            request,
            cancellationToken);

        return Ok(new ApiResponse<IReadOnlyList<ContentIdeaResponse>>
        {
            Data = ideas,
            Message = "Content ideas generated successfully."
        });
    }

    [HttpPost("{id:guid}/generate-post")]
    [ProducesResponseType(typeof(ApiResponse<ContentPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GeneratePost(
        Guid id,
        [FromBody] GenerateContentPostRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var post = await _contentPostService.GenerateFromIdeaAsync(
                id,
                request,
                cancellationToken);

            if (post is null)
            {
                return NotFound(CreateError(
                    "CONTENT_IDEA_NOT_FOUND",
                    "The content idea was not found."));
            }

            return Ok(new ApiResponse<ContentPostResponse>
            {
                Data = post,
                Message = "Content post generated successfully."
            });
        }
        catch (ContentIdeaInvalidStatusException)
        {
            return Conflict(CreateError(
                "CONTENT_IDEA_INVALID_STATUS",
                "Only approved content ideas can be turned into posts."));
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var idea = await _contentIdeaService.ApproveAsync(
                id,
                cancellationToken);

            if (idea is null)
            {
                return NotFound(CreateError(
                    "CONTENT_IDEA_NOT_FOUND",
                    "The content idea was not found."));
            }

            return Ok(new ApiResponse<ContentIdeaResponse>
            {
                Data = idea,
                Message = "Content idea approved."
            });
        }
        catch (InvalidContentIdeaTransitionException)
        {
            return Conflict(CreateError(
                "INVALID_CONTENT_IDEA_TRANSITION",
                "Only suggested content ideas can be approved."));
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var idea = await _contentIdeaService.RejectAsync(
                id,
                cancellationToken);

            if (idea is null)
            {
                return NotFound(CreateError(
                    "CONTENT_IDEA_NOT_FOUND",
                    "The content idea was not found."));
            }

            return Ok(new ApiResponse<ContentIdeaResponse>
            {
                Data = idea,
                Message = "Content idea rejected."
            });
        }
        catch (InvalidContentIdeaTransitionException)
        {
            return Conflict(CreateError(
                "INVALID_CONTENT_IDEA_TRANSITION",
                "Only suggested content ideas can be rejected."));
        }
    }

    private static ApiErrorResponse CreateError(
        string code,
        string message)
    {
        return new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = code,
                Message = message
            },
            TraceId = HttpContextTraceId
        };
    }

    private static string HttpContextTraceId =>
        System.Diagnostics.Activity.Current?.Id ?? string.Empty;
}