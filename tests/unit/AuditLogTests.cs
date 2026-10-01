using System.Net;
using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Models;
using Api.Application.AI.Prompts;
using Api.Application.AuditLogs;
using Api.Application.Authentication;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.Services;
using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Contracts.ContentPost;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class AuditLogTests
{
    [Fact]
    public async Task Approving_an_idea_records_a_content_approved_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateIdeaService(dbContext, workspaceId);
        var idea = SeedIdea(dbContext, workspaceId);

        await service.ApproveAsync(idea.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(AuditAction.ContentApproved, log.Action);
        Assert.Equal("ContentIdea", log.EntityType);
        Assert.Equal(idea.Id, log.EntityId);
        Assert.Equal(workspaceId, log.WorkspaceId);
    }

    [Fact]
    public async Task Rejecting_an_idea_records_a_content_rejected_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateIdeaService(dbContext, workspaceId);
        var idea = SeedIdea(dbContext, workspaceId);

        await service.RejectAsync(idea.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(AuditAction.ContentRejected, log.Action);
        Assert.Equal("ContentIdea", log.EntityType);
    }

    [Fact]
    public async Task An_idea_audit_row_captures_the_status_it_moved_from_and_to()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateIdeaService(dbContext, workspaceId);
        var idea = SeedIdea(dbContext, workspaceId);

        await service.ApproveAsync(idea.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Contains("Suggested", log.OldValues);
        Assert.Contains("Approved", log.NewValues);
    }

    [Fact]
    public async Task A_refused_transition_writes_no_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateIdeaService(dbContext, workspaceId);
        var idea = SeedIdea(dbContext, workspaceId);

        // Approve once to leave the idea in a state it cannot be approved from.
        await service.ApproveAsync(idea.Id, CancellationToken.None);
        dbContext.AuditLogs.RemoveRange(dbContext.AuditLogs);
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidContentIdeaTransitionException>(() =>
            service.ApproveAsync(idea.Id, CancellationToken.None));

        Assert.Empty(dbContext.AuditLogs);
    }

    [Fact]
    public async Task Approving_a_post_records_a_content_approved_audit_row_against_the_post()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreatePostService(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Draft);

        await service.ApproveAsync(post.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(AuditAction.ContentApproved, log.Action);
        Assert.Equal("ContentPost", log.EntityType);
        Assert.Equal(post.Id, log.EntityId);
    }

    [Fact]
    public async Task Rejecting_a_post_records_a_content_rejected_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreatePostService(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Draft);

        await service.RejectAsync(post.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(AuditAction.ContentRejected, log.Action);
        Assert.Equal("ContentPost", log.EntityType);
    }

    [Fact]
    public async Task Scheduling_a_post_records_a_schedule_created_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreatePostService(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);

        var scheduledAt = new DateTimeOffset(DateTime.UtcNow.AddDays(1));
        await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = scheduledAt },
            CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(AuditAction.ScheduleCreated, log.Action);
        Assert.Equal("ContentPost", log.EntityType);
        Assert.Equal(post.Id, log.EntityId);
        Assert.Contains("Scheduled", log.NewValues);
    }

    [Fact]
    public async Task Disconnecting_an_account_records_an_account_disconnected_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateSocialService(dbContext, workspaceId);
        var account = SeedSocialAccount(dbContext, workspaceId);

        await service.DisconnectAsync(account.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(AuditAction.AccountDisconnected, log.Action);
        Assert.Equal("SocialAccount", log.EntityType);
        Assert.Equal(account.Id, log.EntityId);
    }

    [Fact]
    public async Task A_disconnect_audit_row_never_records_credentials()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateSocialService(dbContext, workspaceId);
        var account = SeedSocialAccount(dbContext, workspaceId);
        account.AccessTokenEncrypted = "encrypted-access-token";
        account.RefreshTokenEncrypted = "encrypted-refresh-token";

        await service.DisconnectAsync(account.Id, CancellationToken.None);

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.DoesNotContain("encrypted-access-token", log.NewValues);
        Assert.DoesNotContain("encrypted-refresh-token", log.NewValues);
    }

    [Fact]
    public async Task Reading_returns_only_the_callers_workspace_logs()
    {
        await using var dbContext = CreateDbContext();
        var callerWorkspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        SeedLog(dbContext, callerWorkspaceId, AuditAction.ContentApproved);
        SeedLog(dbContext, otherWorkspaceId, AuditAction.ContentRejected);

        var service = CreateAuditService(dbContext, callerWorkspaceId);
        var logs = await service.GetAsync(null, null, null, CancellationToken.None);

        var log = Assert.Single(logs);
        Assert.Equal(AuditAction.ContentApproved.ToString(), log.Action);
    }

    [Fact]
    public async Task Reading_can_filter_by_action()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedLog(dbContext, workspaceId, AuditAction.ContentApproved);
        SeedLog(dbContext, workspaceId, AuditAction.ScheduleCreated);

        var service = CreateAuditService(dbContext, workspaceId);
        var logs = await service.GetAsync(
            AuditAction.ScheduleCreated, null, null, CancellationToken.None);

        Assert.Equal("ScheduleCreated", Assert.Single(logs).Action);
    }

    [Fact]
    public async Task Reading_can_filter_by_entity_type_and_id()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SeedLog(dbContext, workspaceId, AuditAction.ContentApproved, "ContentPost", targetId);
        SeedLog(dbContext, workspaceId, AuditAction.ContentApproved, "ContentIdea", Guid.NewGuid());

        var service = CreateAuditService(dbContext, workspaceId);
        var logs = await service.GetAsync(
            null, "ContentPost", targetId, CancellationToken.None);

        Assert.Equal(targetId, Assert.Single(logs).ResourceId);
    }

    [Fact]
    public async Task Reading_returns_the_newest_log_first()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var older = SeedLog(dbContext, workspaceId, AuditAction.ContentApproved);
        older.CreatedAt = DateTime.UtcNow.AddHours(-2);
        var newer = SeedLog(dbContext, workspaceId, AuditAction.ContentRejected);
        newer.CreatedAt = DateTime.UtcNow.AddHours(-1);
        await dbContext.SaveChangesAsync();

        var service = CreateAuditService(dbContext, workspaceId);
        var logs = await service.GetAsync(null, null, null, CancellationToken.None);

        Assert.Equal(
            [newer.Id, older.Id],
            logs.Select(log => log.Id).ToArray());
    }

    [Fact]
    public async Task Reading_maps_the_documented_response_fields()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seeded = SeedLog(dbContext, workspaceId, AuditAction.ContentApproved);
        seeded.UserId = userId;
        seeded.IpAddress = IPAddress.Parse("203.0.113.5");
        seeded.UserAgent = "test-agent";
        await dbContext.SaveChangesAsync();

        var service = CreateAuditService(dbContext, workspaceId);
        var log = Assert.Single(await service.GetAsync(null, null, null, CancellationToken.None));

        Assert.Equal(seeded.Id, log.Id);
        Assert.Equal("ContentApproved", log.Action);
        Assert.Equal(seeded.EntityType, log.ResourceType);
        Assert.Equal(seeded.EntityId, log.ResourceId);
        Assert.Equal(userId, log.PerformedBy);
        Assert.Equal("203.0.113.5", log.IpAddress);
        Assert.Equal("test-agent", log.UserAgent);
        Assert.Equal(seeded.CreatedAt, log.CreatedAt);
    }

    [Fact]
    public async Task Reading_an_empty_workspace_returns_an_empty_list()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateAuditService(dbContext, Guid.NewGuid());

        var logs = await service.GetAsync(null, null, null, CancellationToken.None);

        Assert.Empty(logs);
    }

    [Fact]
    public async Task Recording_captures_the_request_address_and_user_agent()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
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
        accessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.5");
        accessor.HttpContext.Request.Headers.UserAgent = "test-agent";

        var service = new AuditLogService(
            dbContext, new CurrentUserService(accessor), accessor);
        await service.RecordAsync(
            AuditAction.ContentApproved, "ContentPost", Guid.NewGuid(),
            cancellationToken: CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal("203.0.113.5", log.IpAddress?.ToString());
        Assert.Equal("test-agent", log.UserAgent);
    }

    [Fact]
    public async Task Recording_attributes_the_acting_user()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim("workspace_id", workspaceId.ToString())
                ], "test"))
            }
        };

        var service = new AuditLogService(
            dbContext, new CurrentUserService(accessor), accessor);
        await service.RecordAsync(
            AuditAction.ContentApproved, "ContentPost", Guid.NewGuid(),
            cancellationToken: CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Equal(userId, Assert.Single(dbContext.AuditLogs).UserId);
    }

    [Fact]
    public async Task Recording_with_an_explicit_workspace_works_without_an_authenticated_user()
    {
        // The social OAuth callback is anonymous: there is no token to read a
        // workspace or a user from, and the workspace comes from signed state.
        await using var dbContext = CreateDbContext();
        var stateWorkspaceId = Guid.NewGuid();
        var service = new AuditLogService(
            dbContext, new CurrentUserService(new HttpContextAccessor()), new HttpContextAccessor());

        await service.RecordAsync(
            stateWorkspaceId, null, AuditAction.AccountConnected, "SocialAccount",
            Guid.NewGuid(), cancellationToken: CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var log = Assert.Single(dbContext.AuditLogs);
        Assert.Equal(stateWorkspaceId, log.WorkspaceId);
        Assert.Null(log.UserId);
    }

    [Fact]
    public async Task A_user_who_cannot_see_a_record_does_not_see_its_audit_row()
    {
        await using var dbContext = CreateDbContext();
        var callerWorkspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var service = CreateIdeaService(dbContext, callerWorkspaceId);
        SeedIdea(dbContext, otherWorkspaceId);

        // The idea belongs to another workspace, so there is nothing to approve.
        var result = await service.ApproveAsync(
            dbContext.ContentIdeas.Single().Id, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(dbContext.AuditLogs);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IAuditLogService CreateAuditService(
        AppDbContext dbContext,
        Guid workspaceId) =>
        AuditLogTestContext.Create(dbContext, workspaceId);

    private static ContentIdeaService CreateIdeaService(
        AppDbContext dbContext,
        Guid workspaceId) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            AuditLogTestContext.Create(dbContext, workspaceId),
            new StubAiProvider("{}"),
            AiRunTestContext.Create(dbContext, workspaceId),
            new ContentIdeaPromptBuilder());

    private static ContentPostService CreatePostService(
        AppDbContext dbContext,
        Guid workspaceId) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            AuditLogTestContext.Create(dbContext, workspaceId),
            new StubAiProvider("{}"),
            AiRunTestContext.Create(dbContext, workspaceId),
            new ContentPostPromptBuilder(),
            new ContentEvaluationPromptBuilder(),
            new ContentRepetitionDetector(),
            NullLogger<ContentPostService>.Instance,
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }),
            Options.Create(new ContentQualityOptions()));

    private static SocialAccountService CreateSocialService(
        AppDbContext dbContext,
        Guid workspaceId) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            AuditLogTestContext.Create(dbContext, workspaceId),
            new SocialPlatformProviderResolver([]),
            new FakeSocialOAuthStateStore(),
            new FakeSocialCredentialProtector(),
            NullLogger<SocialAccountService>.Instance,
            Options.Create(new SocialPlatformOptions()));

    private static ContentIdea SeedIdea(AppDbContext dbContext, Guid workspaceId)
    {
        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Status = ContentIdeaStatus.Suggested,
            Platform = ContentPlatform.LinkedIn,
            Title = "Test idea",
            Topic = "Topic",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ContentIdeas.Add(idea);
        dbContext.SaveChanges();
        return idea;
    }

    private static ContentPost SeedPost(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentPostStatus status)
    {
        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Status = status,
            Platform = ContentPlatform.LinkedIn,
            Content = "Test post",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post;
    }

    private static SocialAccount SeedSocialAccount(AppDbContext dbContext, Guid workspaceId)
    {
        var account = new SocialAccount
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Platform = ContentPlatform.LinkedIn,
            PlatformAccountId = "platform-account-1",
            Username = "someone",
            Status = SocialAccountStatus.Connected,
            AccessTokenEncrypted = "encrypted-access-token",
            ConnectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.SocialAccounts.Add(account);
        dbContext.SaveChanges();
        return account;
    }

    private static AuditLog SeedLog(
        AppDbContext dbContext,
        Guid workspaceId,
        AuditAction action,
        string? entityType = null,
        Guid? entityId = null)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Action = action,
            EntityType = entityType ?? "ContentPost",
            EntityId = entityId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.AuditLogs.Add(log);
        dbContext.SaveChanges();
        return log;
    }

    private sealed class StubAiProvider(string response) : IAiProvider
    {
        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class FakeSocialOAuthStateStore : ISocialOAuthStateStore
    {
        private readonly Dictionary<string, SocialOAuthState> _states = [];

        public Task StoreAsync(
            string state,
            SocialOAuthState value,
            CancellationToken cancellationToken)
        {
            _states[state] = value;
            return Task.CompletedTask;
        }

        public Task<SocialOAuthState?> ConsumeAsync(
            string state,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                _states.Remove(state, out var value) ? value : null);
    }

    private sealed class FakeSocialCredentialProtector : ISocialCredentialProtector
    {
        public string ProtectAccessToken(string accessToken) => accessToken;

        public string ProtectRefreshToken(string refreshToken) => refreshToken;

        public string UnprotectAccessToken(string protectedAccessToken) => protectedAccessToken;

        public string UnprotectRefreshToken(string protectedRefreshToken) => protectedRefreshToken;
    }
}
