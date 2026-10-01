using Api.Application.AuditLogs;
using Api.Contracts.AuditLogs;
using Api.Contracts.Responses;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Workspace audit history. Read-only by design: an audit log that could be
/// edited or deleted through the API would not be evidence of anything.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AuditLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] AuditAction? action,
        [FromQuery] string? resourceType,
        [FromQuery] Guid? resourceId,
        CancellationToken cancellationToken)
    {
        var logs = (await _auditLogService.GetAsync(
            action,
            resourceType,
            resourceId,
            cancellationToken)).ToList();

        return Ok(new ApiResponse<List<AuditLogResponse>>
        {
            Data = logs,
            Message = "Audit logs retrieved successfully."
        });
    }
}
