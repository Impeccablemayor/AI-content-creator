using Api.Application.Authentication;
using Api.Contracts.Responses;
using Api.Contracts.Workspace;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/workspaces")]
public sealed class WorkspacesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;

    public WorkspacesController(
        AppDbContext dbContext,
        CurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(
        CancellationToken cancellationToken)
    {
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == _currentUser.WorkspaceId,
                cancellationToken);

        if (workspace is null)
        {
            return NotFound();
        }

        var membership = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.WorkspaceId == workspace.Id &&
                    x.UserId == _currentUser.UserId,
                cancellationToken);

        if (membership is null)
        {
            return Forbid();
        }

        return Ok(new ApiResponse<WorkspaceResponse>
        {
            Data = new WorkspaceResponse
            {
                Id = workspace.Id,
                Name = workspace.Name,
                Status = workspace.Status.ToString(),
                Role = membership.Role.ToString()
            },
            Message = "Workspace retrieved successfully."
        });
    }

    [HttpPatch("current")]
    public async Task<IActionResult> Update(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(
                x =>
                    x.WorkspaceId == _currentUser.WorkspaceId &&
                    x.UserId == _currentUser.UserId,
                cancellationToken);

        if (membership is null)
        {
            return Forbid();
        }

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(
                x => x.Id == _currentUser.WorkspaceId,
                cancellationToken);

        if (workspace is null)
        {
            return NotFound();
        }

        if (membership.Role is not (
            Domain.Enums.WorkspaceRole.Owner or
            Domain.Enums.WorkspaceRole.Admin))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "INVALID_WORKSPACE_NAME",
                    Message = "Workspace name is required."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        workspace.Name = request.Name.Trim();
        workspace.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<WorkspaceResponse>
        {
            Data = new WorkspaceResponse
            {
                Id = workspace.Id,
                Name = workspace.Name,
                Status = workspace.Status.ToString(),
                Role = membership.Role.ToString()
            },
            Message = "Workspace updated successfully."
        });
    }
}