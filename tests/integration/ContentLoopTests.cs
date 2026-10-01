using System.Net;
using System.Net.Http.Json;
using Api.Application.Security;
using Api.Contracts.Analytics;
using Api.Contracts.Auth;
using Api.Contracts.ContentPost;
using Api.Contracts.Learning;
using Api.Contracts.Responses;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Walks the loop a workspace actually runs: a post is approved and scheduled over
/// HTTP, the worker publishes it and records the platform's identity for it, metrics
/// are pulled back in, reporting reads them, and the analyser turns them into a
/// persisted learning signal.
/// </summary>
/// <remarks>
/// Everything between the seeded draft and the learning signal is the production
/// implementation against a real PostgreSQL. Only the social platform and the model
/// are substituted, because those are the two edges that need the internet.
/// </remarks>
public sealed class ContentLoopTests : IClassFixture<MayorxApiFactory>, IAsyncLifetime
{
    private readonly MayorxApiFactory _factory;

    private HttpClient _client = null!;

    public ContentLoopTests(MayorxApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // Before the first client, because the API binds its configuration while it
        // is building the container, not when the first request arrives.
        _factory.Initialise();
        await _factory.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Scheduled_post_flows_through_publishing_metrics_reporting_and_learning()
    {
        var session = await TestSession.RegisterAsync(_client, "loop");
        using var authenticated = _factory.CreateAuthenticatedClient(session.AccessToken);

        var posts = await SeedTwoDraftPostsAsync(session.WorkspaceId);

        // Approve and schedule over HTTP, so the real transition rules and the real
        // audit trail are in the path.
        foreach (var post in posts)
        {
            var approved = await authenticated.PostAsync(
                $"api/v1/content-posts/{post.Id}/approve",
                content: null);
            TestHttp.EnsureSucceeded(
                approved,
                await TestHttp.ReadAsync<ContentPostResponse>(approved));

            var scheduled = await authenticated.PostAsJsonAsync(
                $"api/v1/content-posts/{post.Id}/schedule",
                new { scheduledAt = DateTimeOffset.UtcNow.AddHours(1) });
            TestHttp.EnsureSucceeded(
                scheduled,
                await TestHttp.ReadAsync<ContentPostResponse>(scheduled));
        }

        // The schedule endpoint refuses a past time, which is correct but would make
        // the run wait. Rewinding the stored time to the past is the only way to put
        // a post in front of the worker without sleeping through the poll interval.
        await BackdateScheduleAsync(posts.Select(post => post.Id));

        var publishRun = await _factory.RunScheduledPostProcessorAsync();

        Assert.Equal(posts.Count, publishRun.Published);

        var published = await LoadPostsAsync(posts.Select(post => post.Id));

        Assert.All(published, post =>
        {
            Assert.Equal(ContentPostStatus.Published, post.Status);
            Assert.NotNull(post.PublishedAt);

            // The platform's own identity for the post, which is what makes the
            // later metrics fetch possible at all.
            Assert.False(string.IsNullOrWhiteSpace(post.PublishedExternalPostId));
            Assert.StartsWith("mayorx-metrics:", post.PublishedExternalPostId);
            Assert.Null(post.PublishFailureReason);
        });

        var syncRun = await _factory.RunAnalyticsSyncAsync();

        Assert.Equal(posts.Count, syncRun.Synced);

        var reported = await TestHttp.ReadAsync<List<PostAnalyticsResponse>>(
            await authenticated.GetAsync("api/v1/analytics/posts"));

        Assert.Equal(posts.Count, reported.Count);

        foreach (var row in reported)
        {
            Assert.NotNull(row.Impressions);
            Assert.NotNull(row.EngagementRate);
            Assert.NotNull(row.PublishedAt);
        }

        // The seeded metrics differ sharply, so the analyser has something real to
        // separate. Both posts share a platform, a pillar, and a publish day, which
        // leaves content type as the one dimension with two groups.
        var run = await TestHttp.ReadAsync<LearningInsightsRunResponse>(
            await authenticated.PostAsync(
                "api/v1/learning/insights",
                content: null));

        Assert.True(run.HasSufficientData);
        Assert.Equal(posts.Count, run.MeasuredPosts);

        var finding = Assert.Single(run.Insights);

        // Both posts share a platform, a pillar, and a publish day, so content type
        // is the one dimension with two groups to compare.
        Assert.Equal("ContentType", finding.Dimension);
        Assert.Equal("CONTENT_TYPE_ENGAGEMENT", finding.Type);
        Assert.Equal(posts.Count, finding.EvidenceCount);
        Assert.Equal(StubAiProvider.Recommendation, finding.Recommendation);
        Assert.Contains("Educational", finding.Observation);
        Assert.Contains("Story", finding.Observation);

        var insights = await TestHttp.ReadAsync<List<LearningInsightResponse>>(
            await authenticated.GetAsync("api/v1/learning/insights"));

        // Read back rather than returned from the run, which proves the signal was
        // persisted and not merely computed.
        var stored = Assert.Single(insights);

        Assert.Equal(finding.Id, stored.Id);
        Assert.Equal(finding.Observation, stored.Observation);

        await using var verification = _factory.Services.CreateAsyncScope();
        var persisted = await verification.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .LearningSignals
            .AsNoTracking()
            .Where(signal => signal.WorkspaceId == session.WorkspaceId)
            .ToListAsync();

        Assert.Equal(finding.Id, Assert.Single(persisted).Id);
    }

    [Fact]
    public async Task One_workspace_cannot_read_another_workspaces_performance_or_signals()
    {
        var owner = await TestSession.RegisterAsync(_client, "owner");
        var stranger = await TestSession.RegisterAsync(_client, "stranger");

        using var ownerClient = _factory.CreateAuthenticatedClient(owner.AccessToken);
        using var strangerClient = _factory.CreateAuthenticatedClient(stranger.AccessToken);

        var posts = await SeedTwoDraftPostsAsync(owner.WorkspaceId);
        await ApproveAndScheduleAsync(ownerClient, posts);
        await BackdateScheduleAsync(posts.Select(post => post.Id));

        await _factory.RunScheduledPostProcessorAsync();
        await _factory.RunAnalyticsSyncAsync();

        await ownerClient.PostAsync("api/v1/learning/insights", content: null);

        var ownerAnalytics = await TestHttp.ReadAsync<List<PostAnalyticsResponse>>(
            await ownerClient.GetAsync("api/v1/analytics/posts"));
        var strangerAnalytics = await TestHttp.ReadAsync<List<PostAnalyticsResponse>>(
            await strangerClient.GetAsync("api/v1/analytics/posts"));

        Assert.NotEmpty(ownerAnalytics);
        Assert.Empty(strangerAnalytics);

        var strangerRun = await TestHttp.ReadAsync<LearningInsightsRunResponse>(
            await strangerClient.PostAsync("api/v1/learning/insights", content: null));

        Assert.False(strangerRun.HasSufficientData);
        Assert.Empty(strangerRun.Insights);

        // Even a direct guess at the identifier must not cross the boundary.
        var crossRead = await strangerClient.GetAsync(
            $"api/v1/content-posts/{posts[0].Id}");

        Assert.Equal(HttpStatusCode.NotFound, crossRead.StatusCode);

        var strangerPosts = await TestHttp.ReadAsync<List<ContentPostResponse>>(
            await strangerClient.GetAsync("api/v1/content-posts"));

        Assert.Empty(strangerPosts);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected_and_still_get_a_correlation_id()
    {
        using var response = await _client.GetAsync("api/v1/analytics/posts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // The header, not the body: an authentication challenge is produced by the
        // framework before any endpoint runs, and it carries no body. The
        // correlation id still has to be on the response, or a rejected call cannot
        // be tied back to its logs.
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(
            response.Headers.GetValues("X-Correlation-Id").Single()));
    }

    private async Task<IReadOnlyList<ContentPost>> SeedTwoDraftPostsAsync(
        Guid workspaceId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<AppDbContext>();
        var protector = services.GetRequiredService<ISocialCredentialProtector>();

        await EnsureLinkedInAccountAsync(dbContext, protector, workspaceId);

        var now = DateTime.UtcNow;
        var pillar = new ContentPillar
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Operations",
            Description = "How the work gets done.",
            Weight = 1m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ContentPillars.Add(pillar);

        // Different content types, and engagement far apart, so exactly one learning
        // dimension has two groups worth comparing.
        var seeds = new[]
        {
            (ContentType: ContentType.Educational, Impressions: 10_000L, Likes: 900L, Comments: 90L),
            (ContentType: ContentType.Story, Impressions: 10_000L, Likes: 40L, Comments: 2L)
        };

        var posts = new List<ContentPost>();

        foreach (var seed in seeds)
        {
            var idea = new ContentIdea
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                ContentPillarId = pillar.Id,
                Status = ContentIdeaStatus.Approved,
                Platform = ContentPlatform.LinkedIn,
                Title = $"{seed.ContentType} idea",
                Topic = "Operations",
                Angle = "A measured angle.",
                Hook = "A hook.",
                Reason = "A reason.",
                SuggestedHashtags = "operations",
                RelevanceScore = 70m,
                EngagementPotentialScore = 70m,
                BrandFitScore = 70m,
                OverallScore = 70m,
                CreatedAt = now,
                UpdatedAt = now
            };

            var post = new ContentPost
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                ContentIdeaId = idea.Id,
                Platform = ContentPlatform.LinkedIn,
                Status = ContentPostStatus.Draft,
                // The marker is how the stub platform knows which metrics to report
                // back for this specific post, once it is published under an id of
                // the platform's own choosing.
                Content =
                    $"{seed.ContentType} body {StubPlatform.MetricsMarker(seed.Impressions, seed.Likes, seed.Comments)}",
                Hook = "A hook.",
                CallToAction = "Read more.",
                Hashtags = ["operations"],
                ContentType = seed.ContentType,
                StorytellingStructure = "Hook, tension, resolution.",
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.ContentIdeas.Add(idea);
            dbContext.ContentPosts.Add(post);
            posts.Add(post);
        }

        await dbContext.SaveChangesAsync();

        return posts;
    }

    private static async Task EnsureLinkedInAccountAsync(
        AppDbContext dbContext,
        ISocialCredentialProtector protector,
        Guid workspaceId)
    {
        var now = DateTime.UtcNow;

        dbContext.SocialAccounts.Add(new SocialAccount
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Platform = ContentPlatform.LinkedIn,
            PlatformAccountId = $"account-{workspaceId:N}",
            Username = "integration-account",
            DisplayName = "Integration Account",
            // Encrypted with the production protector rather than stored as plain
            // text, so the publish path decrypts for real on its way to the platform.
            AccessTokenEncrypted = protector.ProtectAccessToken("stub-access-token"),
            RefreshTokenEncrypted = protector.ProtectRefreshToken("stub-refresh-token"),
            TokenExpiresAt = now.AddDays(30),
            Scopes = ["openid"],
            Status = SocialAccountStatus.Connected,
            ConnectedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task ApproveAndScheduleAsync(
        HttpClient client,
        IReadOnlyList<ContentPost> posts)
    {
        foreach (var post in posts)
        {
            var approved = await client.PostAsync(
                $"api/v1/content-posts/{post.Id}/approve",
                content: null);
            TestHttp.EnsureSucceeded(approved);

            var scheduled = await client.PostAsJsonAsync(
                $"api/v1/content-posts/{post.Id}/schedule",
                new { scheduledAt = DateTimeOffset.UtcNow.AddHours(1) });
            TestHttp.EnsureSucceeded(scheduled);
        }
    }

    private async Task BackdateScheduleAsync(IEnumerable<Guid> postIds)
    {
        var ids = postIds.ToList();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.ContentPosts
            .Where(post => ids.Contains(post.Id))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(post => post.ScheduledAt, DateTime.UtcNow.AddMinutes(-1)));
    }

    private async Task<List<ContentPost>> LoadPostsAsync(IEnumerable<Guid> postIds)
    {
        var ids = postIds.ToList();

        await using var scope = _factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .ContentPosts
            .AsNoTracking()
            .Where(post => ids.Contains(post.Id))
            .ToListAsync();
    }
}
