using Api.Application.Authentication;
using Api.Contracts.Auth;
using Api.Contracts.Responses;
using Api.Contracts.Workspace;
using Api.Domain.Entities;
using Api.Domain.Enums;
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
    private readonly JwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAuthCookieService _authCookieService;

    public WorkspacesController(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        JwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IAuthCookieService authCookieService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _authCookieService = authCookieService;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken)
    {
        var currentWorkspaceId = _currentUser.WorkspaceId;

        // The user's own rows and nothing else. There is no workspace filter on
        // the request to get wrong, because the set of workspaces a caller can
        // see is derived entirely from who they are.
        //
        // Ordered by the same rule login uses, so the first entry in the switcher
        // is the workspace a fresh login would have opened.
        var memberships = await WorkspaceMembershipOrder.Apply(
                _dbContext.WorkspaceMembers
                    .AsNoTracking()
                    .Where(x => x.UserId == _currentUser.UserId))
            .Select(x => new { x.WorkspaceId, x.Role })
            .ToListAsync(cancellationToken);

        var workspaceIds = memberships
            .Select(x => x.WorkspaceId)
            .ToArray();

        // The join is on a list the caller produced themselves rather than a
        // query that could match someone else's rows, and Npgsql translates the
        // containment check into the primary-key lookup it actually needs.
        var workspaces = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(x => workspaceIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Status
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var data = memberships
            .Where(membership => workspaces.ContainsKey(membership.WorkspaceId))
            .Select(membership => new WorkspaceMembershipResponse
            {
                Id = membership.WorkspaceId,
                Name = workspaces[membership.WorkspaceId].Name,
                Status = workspaces[membership.WorkspaceId].Status.ToString(),
                Role = membership.Role.ToString(),
                IsCurrent = membership.WorkspaceId == currentWorkspaceId
            })
            .ToList();

        return Ok(new ApiResponse<List<WorkspaceMembershipResponse>>
        {
            Data = data,
            Message = "Workspaces retrieved successfully."
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
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

        var userId = _currentUser.UserId;
        var now = DateTime.UtcNow;

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Status = WorkspaceStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        // The creator owns it. A workspace with no owner would have nobody who
        // could administer it, invite members, or delete it.
        var membership = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspace.Id,
            Role = WorkspaceRole.Owner,
            JoinedAt = now
        };

        _dbContext.Workspaces.Add(workspace);
        _dbContext.WorkspaceMembers.Add(membership);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // The new workspace becomes the active one immediately, so the response
        // carries a session already scoped to it. The previous workspace is
        // untouched: the user is a member of both, and the switch back is the
        // same call they would make anyway.
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(x => x.Id == userId, cancellationToken);

        var (token, expiresAt) =
            _jwtTokenService.GenerateToken(user, workspace.Id);

        var refreshToken = await _refreshTokenService.IssueAsync(
            userId,
            workspace.Id,
            cancellationToken);

        _authCookieService.Write(
            HttpContext,
            token,
            refreshToken,
            expiresAt);

        // Location points at `current`, not at `/workspaces/{id}`. There is no
        // route that takes a workspace id, so the other form would hand back a
        // URL that answers 404 to whoever followed it. `current` is the
        // resource the response actually describes, and the session cookies on
        // this response already make it the caller's active workspace.
        return Created(
            "/api/v1/workspaces/current",
            new ApiResponse<AuthResponse>
            {
                Data = new AuthResponse
                {
                    ExpiresAt = expiresAt,
                    WorkspaceId = workspace.Id,
                    User = new UserResponse
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    }
                },
                Message = "Workspace created successfully."
            });
    }

    [HttpPost("{workspaceId:guid}/switch")]
    public async Task<IActionResult> Switch(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // One query, and it answers a single question: is this workspace one the
        // caller belongs to?
        //
        // Membership is a predicate on the workspace row rather than a separate
        // lookup, so a caller cannot mint a token for any workspace in the system
        // by guessing its identifier: the row is only produced when both the
        // workspace and the caller's membership in it exist.
        //
        // Existence is part of the same predicate and deliberately shares one
        // failure response with membership. Answering "no such workspace" with
        // 404 and "exists but not yours" with 403 would let any authenticated
        // caller enumerate the system's workspace identifiers by watching which
        // code came back, which is a disclosure the switch endpoint exists to
        // prevent. So a workspace that does not exist and a workspace belonging to
        // someone else are the same answer, and neither is distinguishable from
        // the other.
        //
        // Folding existence in also closes a hole a two-step version leaves open:
        // a membership row whose workspace has been deleted would otherwise pass
        // the membership check and mint a session scoped to a workspace that is
        // not there.
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(x => x.Id == workspaceId &&
                _dbContext.WorkspaceMembers.Any(m =>
                    m.WorkspaceId == x.Id &&
                    m.UserId == userId))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse
                {
                    Error = new ApiError
                    {
                        Code = "WORKSPACE_ACCESS_DENIED",
                        Message = "You are not a member of this workspace."
                    },
                    TraceId = HttpContext.TraceIdentifier
                });
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstAsync(x => x.Id == userId, cancellationToken);

        var (token, expiresAt) =
            _jwtTokenService.GenerateToken(user, workspaceId);

        // Issued like any other authentication, and it follows exactly the same
        // rules as login: the previous session is left alone rather than
        // revoked, so a browser that refreshes a stale tab and switches again
        // does not invalidate a session it is still using.
        var refreshToken = await _refreshTokenService.IssueAsync(
            userId,
            workspaceId,
            cancellationToken);

        _authCookieService.Write(
            HttpContext,
            token,
            refreshToken,
            expiresAt);

        return Ok(new ApiResponse<AuthResponse>
        {
            Data = new AuthResponse
            {
                ExpiresAt = expiresAt,
                WorkspaceId = workspace.Id,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }
            },
            Message = "Workspace switched successfully."
        });
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
            var fallback = await WorkspaceMembershipOrder.Apply(
                    _dbContext.WorkspaceMembers
                        .AsNoTracking()
                        .Where(x => x.UserId == _currentUser.UserId))
                .Select(x => new { x.WorkspaceId, x.Role })
                .FirstOrDefaultAsync(cancellationToken);

            if (fallback is null)
            {
                return NotFound();
            }

            var fallbackWorkspace = await _dbContext.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == fallback.WorkspaceId,
                    cancellationToken);

            if (fallbackWorkspace is null)
            {
                return NotFound();
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstAsync(x => x.Id == _currentUser.UserId, cancellationToken);

            var (token, expiresAt) =
                _jwtTokenService.GenerateToken(user, fallbackWorkspace.Id);

            var refreshToken = await _refreshTokenService.IssueAsync(
                user.Id,
                fallbackWorkspace.Id,
                cancellationToken);

            _authCookieService.Write(
                HttpContext,
                token,
                refreshToken,
                expiresAt);

            return Ok(new ApiResponse<WorkspaceResponse>
            {
                Data = new WorkspaceResponse
                {
                    Id = fallbackWorkspace.Id,
                    Name = fallbackWorkspace.Name,
                    Status = fallbackWorkspace.Status.ToString(),
                    Role = fallback.Role.ToString()
                },
                Message = "Workspace retrieved successfully."
            });
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
