using Api.Application.ContentPosts;
using Api.Contracts.ContentPost;
using Api.Contracts.Responses;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/content-posts")]
public sealed class ContentPostsController : ControllerBase
{
    private readonly IContentPostService _contentPostService;

    public ContentPostsController(IContentPostService contentPostService)
    {
        _contentPostService = contentPostService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] ContentPostStatus? status,
        [FromQuery] ContentPlatform? platform,
        CancellationToken cancellationToken)
    {
        var response = (await _contentPostService.GetAsync(
            status,
            platform,
            cancellationToken)).ToList();

        return Ok(new ApiResponse<List<ContentPostResponse>>
        {
            Data = response,
            Message = "Content posts retrieved successfully."
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var post = await _contentPostService.GetAsync(id, cancellationToken);
        if (post is null)
        {
            return NotFound(CreateError(
                "CONTENT_POST_NOT_FOUND",
                "The content post was not found."));
        }

        return Ok(new ApiResponse<ContentPostResponse>
        {
            Data = post,
            Message = "Content post retrieved successfully."
        });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateContentPostRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var post = await _contentPostService.UpdateAsync(
                id,
                request,
                cancellationToken);
            if (post is null)
            {
                return NotFound(CreateError(
                    "CONTENT_POST_NOT_FOUND",
                    "The content post was not found."));
            }

            return Ok(new ApiResponse<ContentPostResponse>
            {
                Data = post,
                Message = "Content post updated successfully."
            });
        }
        catch (InvalidContentPostContentException exception)
        {
            return BadRequest(CreateError("INVALID_CONTENT_POST_CONTENT", exception.Message));
        }
        catch (InvalidContentPostTransitionException exception)
        {
            return Conflict(CreateError("INVALID_CONTENT_POST_TRANSITION", exception.Message));
        }
    }

    [HttpPost("{id:guid}/regenerate")]
    [ProducesResponseType(typeof(ApiResponse<ContentPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Regenerate(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var post = await _contentPostService.RegenerateAsync(id, cancellationToken);
            if (post is null)
            {
                return NotFound(CreateError(
                    "CONTENT_POST_NOT_FOUND",
                    "The content post was not found."));
            }

            return Ok(new ApiResponse<ContentPostResponse>
            {
                Data = post,
                Message = "Content post regenerated successfully."
            });
        }
        catch (InvalidContentPostTransitionException exception)
        {
            return Conflict(CreateError("INVALID_CONTENT_POST_TRANSITION", exception.Message));
        }
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<ContentPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var post = await _contentPostService.ApproveAsync(id, cancellationToken);
            if (post is null)
            {
                return NotFound(CreateError(
                    "CONTENT_POST_NOT_FOUND",
                    "The content post was not found."));
            }

            return Ok(new ApiResponse<ContentPostResponse>
            {
                Data = post,
                Message = "Content post approved."
            });
        }
        catch (InvalidContentPostTransitionException exception)
        {
            return Conflict(CreateError("INVALID_CONTENT_POST_TRANSITION", exception.Message));
        }
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ContentPostResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var post = await _contentPostService.RejectAsync(id, cancellationToken);
            if (post is null)
            {
                return NotFound(CreateError(
                    "CONTENT_POST_NOT_FOUND",
                    "The content post was not found."));
            }

            return Ok(new ApiResponse<ContentPostResponse>
            {
                Data = post,
                Message = "Content post rejected."
            });
        }
        catch (InvalidContentPostTransitionException exception)
        {
            return Conflict(CreateError("INVALID_CONTENT_POST_TRANSITION", exception.Message));
        }
    }

    [HttpPost("{id:guid}/evaluate")]
    [ProducesResponseType(typeof(ApiResponse<ContentEvaluationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Evaluate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var evaluation = await _contentPostService.EvaluateAsync(id, cancellationToken);
        if (evaluation is null)
        {
            return NotFound(CreateError(
                "CONTENT_POST_NOT_FOUND",
                "The content post was not found."));
        }

        return Ok(new ApiResponse<ContentEvaluationResponse>
        {
            Data = evaluation,
            Message = "Content post evaluated successfully."
        });
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
