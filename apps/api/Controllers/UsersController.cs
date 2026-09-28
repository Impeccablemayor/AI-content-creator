using Api.Application.Authentication;
using Api.Contracts.Auth;
using Api.Contracts.Responses;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;

    public UsersController(
        AppDbContext dbContext,
        CurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == _currentUser.UserId,
                cancellationToken);

        if (user is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "USER_NOT_FOUND",
                    Message = "The authenticated user could not be found."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var membership = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.UserId == user.Id &&
                    x.WorkspaceId == _currentUser.WorkspaceId,
                cancellationToken);

        if (membership is null)
        {
            return Forbid();
        }

        return Ok(new ApiResponse<object>
        {
            Data = new
            {
                user = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                },
                workspaceId = membership.WorkspaceId,
                role = membership.Role.ToString()
            },
            Message = "Current user retrieved successfully."
        });
    }
}