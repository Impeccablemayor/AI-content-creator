using System.Reflection;
using Api.Application.Analytics;
using Api.Contracts.Analytics;
using Api.Contracts.Responses;
using Api.Controllers;
using Api.Domain.Enums;
using Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsEndpointTests
{
    [Fact]
    public void Analytics_controller_requires_authentication()
    {
        var controller = typeof(AnalyticsController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/analytics", controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    /// <summary>
    /// Analytics is a read surface, so it must not expose a verb that can
    /// change anything.
    /// </summary>
    [Fact]
    public void Every_analytics_route_is_a_get()
    {
        var actions = typeof(AnalyticsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpGetAttribute>() is not null)
            .ToList();

        Assert.Equal(3, actions.Count);
        Assert.All(actions, action =>
            Assert.Null(action.GetCustomAttribute<HttpPostAttribute>()));
        Assert.All(actions, action =>
            Assert.Null(action.GetCustomAttribute<HttpPatchAttribute>()));
        Assert.All(actions, action =>
            Assert.Null(action.GetCustomAttribute<HttpPutAttribute>()));
        Assert.All(actions, action =>
            Assert.Null(action.GetCustomAttribute<HttpDeleteAttribute>()));
    }

    [Fact]
    public void Overview_is_a_get_with_platform_and_period_filters()
    {
        var overview = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetOverview));

        Assert.NotNull(overview);
        Assert.Equal("overview", overview!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(
            [typeof(ContentPlatform?), typeof(DateTime?), typeof(DateTime?), typeof(Guid?), typeof(CancellationToken)],
            overview.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void Posts_collection_is_a_get_with_the_same_filters()
    {
        var posts = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetPosts));

        Assert.NotNull(posts);
        Assert.Equal("posts", posts!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(
            [typeof(ContentPlatform?), typeof(DateTime?), typeof(DateTime?), typeof(Guid?), typeof(CancellationToken)],
            posts.GetParameters().Select(parameter => parameter.ParameterType));
    }

    /// <summary>
    /// The API specification names this parameter {publicationId}. Publishing
    /// stores its outcome on the content post and there is no publication
    /// entity, so the route is keyed by content post id and guarded as a guid.
    /// </summary>
    [Fact]
    public void Single_post_is_a_get_on_a_guarded_content_post_id()
    {
        var post = typeof(AnalyticsController)
            .GetMethod(nameof(AnalyticsController.GetPost));

        Assert.NotNull(post);
        Assert.Equal("posts/{id:guid}", post!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(typeof(Guid), post.GetParameters()[0].ParameterType);
    }

    [Fact]
    public async Task Overview_returns_the_shared_envelope_with_the_overview_payload()
    {
        var result = await new AnalyticsController(StubAnalytics.WithOverview())
            .GetOverview(null, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<AnalyticsOverviewResponse>>(ok.Value);

        Assert.Equal(3, envelope.Data.PostsPublished);
        Assert.Equal("Analytics overview retrieved successfully.", envelope.Message);
    }

    [Fact]
    public async Task Posts_returns_the_shared_envelope_with_a_list_payload()
    {
        var result = await new AnalyticsController(StubAnalytics.WithPosts())
            .GetPosts(ContentPlatform.X, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<List<PostAnalyticsResponse>>>(ok.Value);

        Assert.Single(envelope.Data);
    }

    [Fact]
    public async Task Single_post_returns_200_with_the_envelope()
    {
        var id = Guid.NewGuid();
        var result = await new AnalyticsController(StubAnalytics.WithPost(id))
            .GetPost(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<PostAnalyticsResponse>>(ok.Value);

        Assert.Equal(id, envelope.Data.ContentPostId);
    }

    [Fact]
    public async Task Single_post_returns_404_when_there_is_no_published_post_to_report_on()
    {
        var result = await new AnalyticsController(StubAnalytics.WithPostMissing())
            .GetPost(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);

        Assert.Equal("ANALYTICS_POST_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task An_inverted_period_is_rejected_before_the_service_runs()
    {
        var stub = StubAnalytics.WithOverview();
        var controller = new AnalyticsController(stub);

        await Assert.ThrowsAsync<InvalidAnalyticsPeriodException>(() =>
            controller.GetOverview(
                null,
                Anchor.AddDays(5),
                Anchor,
                null,
                CancellationToken.None));

        Assert.Equal(0, stub.OverviewCalls);
    }

    [Fact]
    public async Task An_inverted_period_maps_to_a_bad_request_error_code()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidAnalyticsPeriodException(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GlobalExceptionMiddleware>.Instance,
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.AspNetCore.Mvc.JsonOptions()));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var payload = new StreamReader(context.Response.Body).ReadToEnd();

        // Read as raw JSON rather than deserialized, because the property casing
        // is part of what this asserts: the error envelope has to look the same
        // to a client as every other response the API returns.
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        var error = document.RootElement.GetProperty("error");

        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("INVALID_ANALYTICS_PERIOD", error.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload));
        Assert.NotEqual(
            string.Empty,
            document.RootElement.GetProperty("traceId").GetString());
    }

    private static readonly DateTime Anchor = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Records what the controller asked for so the tests can prove the filters
    /// and the period guard reach the service unchanged.
    /// </summary>
    private sealed class StubAnalytics : IAnalyticsService
    {
        private readonly AnalyticsOverviewResponse _overview;
        private readonly IReadOnlyList<PostAnalyticsResponse> _posts;
        private readonly Guid? _postId;
        private readonly bool _postMissing;

        public int OverviewCalls { get; private set; }

        public AnalyticsQuery? LastQuery { get; private set; }

        private StubAnalytics(
            AnalyticsOverviewResponse overview,
            IReadOnlyList<PostAnalyticsResponse> posts,
            Guid? postId,
            bool postMissing)
        {
            _overview = overview;
            _posts = posts;
            _postId = postId;
            _postMissing = postMissing;
        }

        public static StubAnalytics WithOverview() =>
            new(new AnalyticsOverviewResponse { PostsPublished = 3 }, [], null, false);

        public static StubAnalytics WithPosts() =>
            new(
                new AnalyticsOverviewResponse(),
                [new PostAnalyticsResponse { ContentPostId = Guid.NewGuid(), Platform = "X" }],
                null,
                false);

        public static StubAnalytics WithPost(Guid id) =>
            new(
                new AnalyticsOverviewResponse(),
                [],
                id,
                false);

        public static StubAnalytics WithPostMissing() =>
            new(new AnalyticsOverviewResponse(), [], null, true);

        public Task<AnalyticsOverviewResponse> GetOverviewAsync(
            AnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            OverviewCalls++;
            LastQuery = query;
            return Task.FromResult(_overview);
        }

        public Task<IReadOnlyList<PostAnalyticsResponse>> GetPostsAsync(
            AnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(_posts);
        }

        public Task<PostAnalyticsResponse?> GetPostAsync(
            Guid contentPostId,
            CancellationToken cancellationToken = default)
        {
            if (_postMissing)
            {
                return Task.FromResult<PostAnalyticsResponse?>(null);
            }

            return Task.FromResult<PostAnalyticsResponse?>(
                new PostAnalyticsResponse { ContentPostId = _postId!.Value });
        }
    }
}
