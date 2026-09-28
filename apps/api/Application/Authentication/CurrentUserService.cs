using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Api.Application.Authentication;

public sealed class CurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(value, out var userId))
            {
                throw new UnauthorizedAccessException(
                    "Authenticated user ID is missing.");
            }

            return userId;
        }
    }

    public Guid WorkspaceId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User
                .FindFirstValue("workspace_id");

            if (!Guid.TryParse(value, out var workspaceId))
            {
                throw new UnauthorizedAccessException(
                    "Authenticated workspace ID is missing.");
            }

            return workspaceId;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?
            .User
            .Identity?
            .IsAuthenticated == true;
}