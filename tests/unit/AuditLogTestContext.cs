using System.Security.Claims;
using Api.Application.AuditLogs;
using Api.Application.Authentication;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Api.Tests;

/// <summary>
/// Builds the audit service that the application services under test write
/// through, so tests exercise the real recording path against the same
/// <see cref="AppDbContext"/> as the change it describes.
/// </summary>
internal static class AuditLogTestContext
{
    public static IAuditLogService Create(AppDbContext dbContext, Guid workspaceId) =>
        Create(dbContext, CreateAccessor(workspaceId));

    public static IAuditLogService Create(
        AppDbContext dbContext,
        IHttpContextAccessor accessor) =>
        new AuditLogService(dbContext, new CurrentUserService(accessor), accessor);

    public static HttpContextAccessor CreateAccessor(Guid workspaceId) =>
        new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim("workspace_id", workspaceId.ToString())
                ], "test"))
            }
        };
}
