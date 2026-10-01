using Api.Application.Learning;
using Api.Contracts.Learning;
using Api.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/learning")]
public sealed class LearningController : ControllerBase
{
    private readonly ILearningService _learningService;

    public LearningController(ILearningService learningService)
    {
        _learningService = learningService;
    }

    /// <summary>
    /// Stored learning signals, newest first.
    /// </summary>
    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var insights = (await _learningService.GetInsightsAsync(
            limit ?? LearningService.MaxInsightsLimit,
            cancellationToken)).ToList();

        return Ok(new ApiResponse<List<LearningInsightResponse>>
        {
            Data = insights,
            Message = "Learning insights retrieved successfully."
        });
    }

    /// <summary>
    /// Runs the loop once over the workspace's measured posts.
    /// </summary>
    /// <remarks>
    /// Returns 200 with <c>hasSufficientData: false</c> and an empty list when
    /// there is not enough measured history to compare. That is a completed run
    /// with nothing to say, not a failure.
    /// </remarks>
    [HttpPost("insights")]
    public async Task<IActionResult> GenerateInsights(CancellationToken cancellationToken)
    {
        var response = await _learningService.GenerateInsightsAsync(cancellationToken);

        return Ok(new ApiResponse<LearningInsightsRunResponse>
        {
            Data = response,
            Message = response.HasSufficientData
                ? "Learning insights generated successfully."
                : "Not enough measured performance to generate learning insights yet."
        });
    }
}
