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

    public AuthController(
        AppDbContext dbContext,
        JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
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

        return Ok(new ApiResponse<AuthResponse>
        {
            Data = new AuthResponse
            {
                AccessToken = token,
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

        var membership = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(
                x => x.UserId == user.Id,
                cancellationToken);

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

        return Ok(new ApiResponse<AuthResponse>
        {
            Data = new AuthResponse
            {
                AccessToken = token,
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
}