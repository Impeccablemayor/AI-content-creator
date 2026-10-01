using Api.Application.SocialAccounts;
using Api.Contracts.Responses;
using Api.Contracts.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/social")]
public sealed class SocialAccountsController : ControllerBase
{
    private readonly ISocialAccountService _socialAccountService;

    public SocialAccountsController(ISocialAccountService socialAccountService)
    {
        _socialAccountService = socialAccountService;
    }

    /// <summary>
    /// Lists the calling workspace's connected social accounts. Never includes
    /// credentials.
    /// </summary>
    [HttpGet("accounts")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<SocialAccountResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(
        CancellationToken cancellationToken)
    {
        var accounts = (await _socialAccountService.GetAsync(cancellationToken)).ToList();

        return Ok(new ApiResponse<List<SocialAccountResponse>>
        {
            Data = accounts,
            Message = "Social accounts retrieved successfully."
        });
    }

    /// <summary>
    /// Returns the provider authorization URL for a platform.
    /// </summary>
    [HttpGet("{platform}/connect")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SocialConnectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Connect(
        string platform,
        CancellationToken cancellationToken)
    {
        var connect = await _socialAccountService.CreateAuthorizationAsync(
            platform,
            cancellationToken);

        return Ok(new ApiResponse<SocialConnectResponse>
        {
            Data = connect,
            Message = "Social authorization URL created."
        });
    }

    /// <summary>
    /// OAuth callback target.
    /// </summary>
    /// <remarks>
    /// Anonymous by design: the platform redirects the browser here without an
    /// Authorization header. The workspace is taken from the stored single-use
    /// OAuth state instead of the caller.
    /// </remarks>
    [HttpGet("{platform}/callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SocialAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Callback(
        string platform,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(CreateError(
                "SOCIAL_OAUTH_DENIED",
                "The social platform did not authorize the connection."));
        }

        var account = await _socialAccountService.CompleteAsync(
            platform,
            code ?? string.Empty,
            state ?? string.Empty,
            cancellationToken);

        return Ok(new ApiResponse<SocialAccountResponse>
        {
            Data = account!,
            Message = "Social account connected successfully."
        });
    }

    /// <summary>
    /// Disconnects a social account and destroys its stored credentials.
    /// </summary>
    [HttpDelete("accounts/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SocialAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disconnect(
        Guid id,
        CancellationToken cancellationToken)
    {
        var account = await _socialAccountService.DisconnectAsync(id, cancellationToken);
        if (account is null)
        {
            return NotFound(CreateError(
                "SOCIAL_ACCOUNT_NOT_FOUND",
                "The social account was not found."));
        }

        return Ok(new ApiResponse<SocialAccountResponse>
        {
            Data = account,
            Message = "Social account disconnected."
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
