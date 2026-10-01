using Api.Application.ContentPillars;
using Api.Contracts.ContentPillar;
using Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Content pillars, the recurring themes content ideas are attributed to.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/strategy/pillars")]
public sealed class ContentPillarsController : ControllerBase
{
    private readonly IContentPillarService _contentPillarService;

    public ContentPillarsController(IContentPillarService contentPillarService)
    {
        _contentPillarService = contentPillarService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ContentPillarResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var pillars = (await _contentPillarService.GetAsync(
            isActive,
            cancellationToken)).ToList();

        return Ok(new ApiResponse<List<ContentPillarResponse>>
        {
            Data = pillars,
            Message = "Content pillars retrieved successfully."
        });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContentPillarResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var pillar = await _contentPillarService.GetAsync(id, cancellationToken);
        if (pillar is null)
        {
            return NotFound(CreateError(
                "CONTENT_PILLAR_NOT_FOUND",
                "The content pillar was not found."));
        }

        return Ok(new ApiResponse<ContentPillarResponse>
        {
            Data = pillar,
            Message = "Content pillar retrieved successfully."
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ContentPillarResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContentPillarRequest request,
        CancellationToken cancellationToken)
    {
        var pillar = await _contentPillarService.CreateAsync(request, cancellationToken);

        return Created(
            $"/api/v1/strategy/pillars/{pillar.Id}",
            new ApiResponse<ContentPillarResponse>
            {
                Data = pillar,
                Message = "Content pillar created successfully."
            });
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ContentPillarResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateContentPillarRequest request,
        CancellationToken cancellationToken)
    {
        var pillar = await _contentPillarService.UpdateAsync(id, request, cancellationToken);
        if (pillar is null)
        {
            return NotFound(CreateError(
                "CONTENT_PILLAR_NOT_FOUND",
                "The content pillar was not found."));
        }

        return Ok(new ApiResponse<ContentPillarResponse>
        {
            Data = pillar,
            Message = "Content pillar updated successfully."
        });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _contentPillarService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(CreateError(
                "CONTENT_PILLAR_NOT_FOUND",
                "The content pillar was not found."));
        }

        return NoContent();
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
