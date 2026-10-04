using Api.Application.Authentication;
using Api.Contracts.Auth;
using Api.Contracts.Responses;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAuthCookieService _authCookieService;

    public AuthController(
        AppDbContext dbContext,
        JwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IAuthCookieService authCookieService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _authCookieService = authCookieService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .AnyAsync(
                user => user.Email == email,
                cancellationToken);

        if (existingUser)
        {
            return Conflict(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "EMAIL_ALREADY_EXISTS",
                    Message = "An account with this email already exists."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            TimeZone = "UTC",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.WorkspaceName.Trim(),
            Status = WorkspaceStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var membership = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            WorkspaceId = workspace.Id,
            Role = WorkspaceRole.Owner,
            JoinedAt = now
        };

        _dbContext.Users.Add(user);
        _dbContext.Workspaces.Add(workspace);
        _dbContext.WorkspaceMembers.Add(membership);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) =
            _jwtTokenService.GenerateToken(
                user,
                workspace.Id);

        var refreshToken = await _refreshTokenService.IssueAsync(
            user.Id,
            workspace.Id,
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
            Message = "Account created successfully."
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email == email,
                cancellationToken);

        if (user is null ||
            !BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            return Unauthorized(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "INVALID_CREDENTIALS",
                    Message = "Invalid email or password."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "ACCOUNT_INACTIVE",
                    Message = "This account is inactive."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        // The oldest membership wins, with the identifier as the tie-break. The
        // rule itself, and why it is ordered that way, is on
        // WorkspaceMembershipOrder; the workspace listing uses the same one, so a
        // switcher cannot disagree with login about which workspace is current.
        var membership = await WorkspaceMembershipOrder.Apply(
                _dbContext.WorkspaceMembers
                    .AsNoTracking()
                    .Where(x => x.UserId == user.Id))
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return Unauthorized(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "WORKSPACE_NOT_FOUND",
                    Message = "No workspace is associated with this account."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var (token, expiresAt) =
            _jwtTokenService.GenerateToken(
                user,
                membership.WorkspaceId);

        var refreshToken = await _refreshTokenService.IssueAsync(
            user.Id,
            membership.WorkspaceId,
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
                WorkspaceId = membership.WorkspaceId,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }
            },
            Message = "Login successful."
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        CancellationToken cancellationToken)
    {
        var presentedToken = _authCookieService.ReadRefreshToken(
            HttpContext);

        // A failed refresh leaves the browser holding a credential the server
        // has already consumed. Left in place, the next automatic refresh would
        // present it again, which rotation reports as a replay and answers by
        // revoking the live session the user is actually using. So the cookie is
        // cleared whenever the presented token does not produce a new session.
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            _authCookieService.Clear(HttpContext);

            return Unauthorized(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "INVALID_REFRESH_TOKEN",
                    Message = "No refresh session was presented."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        RefreshedSession session;

        try
        {
            session = await _refreshTokenService.RefreshAsync(
                presentedToken,
                cancellationToken);
        }
        catch (Exception exception) when (exception is
            InvalidRefreshTokenException or
            ExpiredRefreshTokenException or
            RevokedRefreshTokenException or
            ReusedRefreshTokenException or
            RefreshTokenRotationConflictException)
        {
            _authCookieService.Clear(HttpContext);

            throw;
        }

        _authCookieService.Write(
            HttpContext,
            session.AccessToken,
            session.RefreshToken,
            session.AccessTokenExpiresAt);

        return Ok(new ApiResponse<AuthResponse>
        {
            Data = new AuthResponse
            {
                ExpiresAt = session.AccessTokenExpiresAt,
                WorkspaceId = session.WorkspaceId,
                User = new UserResponse
                {
                    Id = session.UserId,
                    Email = session.Email,
                    FirstName = session.FirstName,
                    LastName = session.LastName
                }
            },
            Message = "Token refreshed successfully."
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        var presentedToken = _authCookieService.ReadRefreshToken(
            HttpContext);

        // Revocation failures are deliberately not caught. A logout that could
        // not be persisted must not report success.
        await _refreshTokenService.RevokeAsync(
            presentedToken ?? string.Empty,
            cancellationToken);

        _authCookieService.Clear(HttpContext);

        return Ok(new ApiResponse<object>
        {
            Data = new { },
            Message = "Logged out successfully."
        });
    }
}