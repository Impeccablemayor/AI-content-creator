using Api.Application.AI;
using Api.Application.Authentication;
using Api.Configuration;
using Api.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

/// <summary>
/// Builds a real <see cref="AiRunRecorder"/> over the same
/// <see cref="AppDbContext"/> as the service under test, so tests exercise the
/// real recording path rather than a stub that records nothing.
/// </summary>
internal static class AiRunTestContext
{
    public static IAiRunRecorder Create(AppDbContext dbContext, Guid workspaceId) =>
        new AiRunRecorder(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }),
            NullLogger<AiRunRecorder>.Instance);
}
