using System.Net;
using System.Text;
using System.Web;
using Api.Application.AnalyticsSync;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class SocialPlatformMetricsProviderTests
{
    private const string AccessToken = "metrics-access-token";

    [Fact]
    public async Task X_reads_public_and_private_metrics_in_one_request()
    {
        Uri? uri = null;
        string? authorization = null;
        var provider = CreateXProvider((request, _) =>
        {
            uri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            return JsonResponseAsync(
                """
                {
                  "data": {
                    "id": "1900",
                    "public_metrics": {
                      "retweet_count": 3,
                      "reply_count": 2,
                      "like_count": 7,
                      "quote_count": 4,
                      "impression_count": 500
                    },
                    "non_public_metrics": {
                      "impression_count": 1234,
                      "url_link_clicks": 11,
                      "user_profile_clicks": 6
                    }
                  }
                }
                """);
        });

        var metrics = await provider.FetchAsync(AccessToken, "1900", CancellationToken.None);

        Assert.Equal(1234, metrics.Impressions);
        Assert.Equal(7, metrics.Likes);
        Assert.Equal(2, metrics.Comments);
        Assert.Equal(7, metrics.Shares);
        Assert.Equal(11, metrics.Clicks);
        Assert.Equal(6, metrics.ProfileVisits);
        Assert.Equal(
            "https://api.twitter.com/2/tweets/1900?tweet.fields=public_metrics,non_public_metrics",
            uri?.ToString());
        Assert.Equal($"Bearer {AccessToken}", authorization);
    }

    /// <summary>
    /// X has no per-post follower gain. Reporting zero would be a measurement
    /// the platform never made.
    /// </summary>
    [Fact]
    public async Task X_leaves_a_metric_it_does_not_expose_null_rather_than_zero()
    {
        var provider = CreateXProvider((_, _) => JsonResponseAsync(
            """{"data":{"public_metrics":{"like_count":1}}}"""));

        var metrics = await provider.FetchAsync(AccessToken, "1", CancellationToken.None);

        Assert.Null(metrics.FollowersGained);
        Assert.Null(metrics.Impressions);
        Assert.Null(metrics.Clicks);
        Assert.Equal(1, metrics.Likes);
    }

    /// <summary>
    /// A post nobody has clicked still has a click count of zero. Collapsing that
    /// into null would make a real measurement look like a missing one.
    /// </summary>
    [Fact]
    public async Task A_reported_zero_is_kept_as_zero()
    {
        var provider = CreateXProvider((_, _) => JsonResponseAsync(
            """{"data":{"non_public_metrics":{"url_link_clicks":0}}}"""));

        var metrics = await provider.FetchAsync(AccessToken, "1", CancellationToken.None);

        Assert.Equal(0, metrics.Clicks);
    }

    [Fact]
    public async Task LinkedIn_requests_every_mapped_metric_and_returns_them_as_one_snapshot()
    {
        var requested = new List<string>();
        var provider = CreateLinkedInProvider((request, _) =>
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            requested.Add(query["queryType"]!);
            return JsonResponseAsync(
                $$"""{"elements":[{"count":{{requested.Count}}}]}""");
        });

        var metrics = await provider.FetchAsync(
            AccessToken,
            "urn:li:share:42",
            CancellationToken.None);

        Assert.Equal(
            [
                "IMPRESSION",
                "REACTION",
                "COMMENT",
                "RESHARE",
                "LINK_CLICKS",
                "PROFILE_VIEW_FROM_CONTENT",
                "FOLLOWER_GAINED_FROM_CONTENT"
            ],
            requested);
        Assert.Equal(1, metrics.Impressions);
        Assert.Equal(7, metrics.FollowersGained);
    }

    /// <summary>
    /// LinkedIn answers one metric per call, so a post costs several requests.
    /// The count is asserted because the batch size and the resync interval are
    /// sized around it.
    /// </summary>
    [Fact]
    public async Task LinkedIn_sends_the_share_entity_and_the_required_version_headers()
    {
        var queries = new List<string>();
        string? protocolVersion = null;
        string? apiVersion = null;
        var provider = CreateLinkedInProvider((request, _) =>
        {
            queries.Add(request.RequestUri!.Query);
            protocolVersion = request.Headers.GetValues("X-Restli-Protocol-Version").Single();
            apiVersion = request.Headers.GetValues("LinkedIn-Version").Single();
            return JsonResponseAsync("""{"elements":[{"count":0}]}""");
        });

        await provider.FetchAsync(AccessToken, "urn:li:share:42", CancellationToken.None);

        Assert.All(
            queries,
            query => Assert.Contains("q=entity", query, StringComparison.Ordinal));
        Assert.All(
            queries,
            query => Assert.Contains(
                "%28share%3Aurn%3Ali%3Ashare%3A42%29",
                query,
                StringComparison.Ordinal));
        Assert.Equal("2.0.0", protocolVersion);
        Assert.Equal("202601", apiVersion);
    }

    [Fact]
    public async Task LinkedIn_treats_a_metric_with_no_result_as_unavailable_not_as_zero()
    {
        var provider = CreateLinkedInProvider((request, _) =>
        {
            var queryType = System.Web.HttpUtility
                .ParseQueryString(request.RequestUri!.Query)["queryType"];

            return Task.FromResult(queryType == "IMPRESSION"
                ? JsonResponse("""{"elements":[{"count":12}]}""")
                : JsonResponse("""{"elements":[]}"""));
        });

        var metrics = await provider.FetchAsync(AccessToken, "42", CancellationToken.None);

        Assert.Equal(12, metrics.Impressions);
        Assert.Null(metrics.Likes);
        Assert.Null(metrics.FollowersGained);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Rejected_credentials_raise_an_authentication_failure(HttpStatusCode statusCode)
    {
        var provider = CreateXProvider((_, _) =>
            Task.FromResult(Status(statusCode, "{\"errors\":[]}")));

        await Assert.ThrowsAsync<SocialProviderAuthenticationException>(() =>
            provider.FetchAsync(AccessToken, "1", CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Transient_provider_failures_are_reported_as_unavailable(HttpStatusCode statusCode)
    {
        var provider = CreateXProvider((_, _) => Task.FromResult(Status(statusCode, "down")));

        await Assert.ThrowsAsync<SocialProviderUnavailableException>(() =>
            provider.FetchAsync(AccessToken, "1", CancellationToken.None));
    }

    [Fact]
    public async Task A_client_error_is_reported_as_a_rejected_request()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(
            Status(HttpStatusCode.BadRequest, """{"message":"no access"}""")));

        await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            provider.FetchAsync(AccessToken, "42", CancellationToken.None));
    }

    /// <summary>
    /// A metrics failure is logged by the sync worker, so the body must never
    /// reach the exception message.
    /// </summary>
    [Fact]
    public async Task Failure_messages_never_carry_the_provider_response_body()
    {
        const string secret = "sensitive-post-echoed-by-provider";
        var provider = CreateXProvider((_, _) => Task.FromResult(
            Status(HttpStatusCode.BadRequest, $$"""{"title":"{{secret}}"}""")));

        var exception = await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            provider.FetchAsync(AccessToken, "1", CancellationToken.None));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(AccessToken, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_success_body_that_is_not_json_is_treated_as_invalid()
    {
        var provider = CreateXProvider((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "text/html")
            }));

        await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            provider.FetchAsync(AccessToken, "1", CancellationToken.None));
    }

    [Fact]
    public async Task A_success_response_for_a_different_post_is_treated_as_invalid()
    {
        var provider = CreateXProvider((_, _) => JsonResponseAsync("""{"errors":[]}"""));

        await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            provider.FetchAsync(AccessToken, "1", CancellationToken.None));
    }

    [Fact]
    public async Task A_post_id_is_escaped_rather_than_being_appended_raw()
    {
        Uri? uri = null;
        var provider = CreateXProvider((request, _) =>
        {
            uri = request.RequestUri;
            return JsonResponseAsync("""{"data":{"public_metrics":{"like_count":1}}}""");
        });

        await provider.FetchAsync(AccessToken, "../../users/me", CancellationToken.None);

        Assert.Contains("/2/tweets/..%2F..%2Fusers%2Fme", uri?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_platform_without_client_configuration_cannot_read_metrics()
    {
        var options = CreateLinkedInOptions();
        options.ClientSecret = string.Empty;

        var provider = new LinkedInSocialPlatformMetricsProvider(
            new HttpClient(new StubHttpMessageHandler((_, _) => JsonResponseAsync("{}"))),
            Options.Create(options));

        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public void The_resolver_returns_the_provider_for_a_supported_platform()
    {
        var resolver = new SocialPlatformMetricsProviderResolver(
            [CreateLinkedInProvider(), CreateXProvider()]);

        Assert.Equal(ContentPlatform.LinkedIn, resolver.Get(ContentPlatform.LinkedIn).Platform);
        Assert.Equal(ContentPlatform.X, resolver.Get(ContentPlatform.X).Platform);
    }

    [Fact]
    public void The_resolver_rejects_a_platform_it_cannot_measure()
    {
        var resolver = new SocialPlatformMetricsProviderResolver([CreateXProvider()]);

        Assert.Throws<UnsupportedSocialPlatformException>(() =>
            resolver.Get(ContentPlatform.LinkedIn));
    }

    private static XSocialPlatformMetricsProvider CreateXProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null) =>
        new(
            new HttpClient(new StubHttpMessageHandler(
                handler ?? ((_, _) => JsonResponseAsync("""{"data":{}}""")))),
            Options.Create(CreateXOptions()));

    private static LinkedInSocialPlatformMetricsProvider CreateLinkedInProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null) =>
        new(
            new HttpClient(new StubHttpMessageHandler(
                handler ?? ((_, _) => JsonResponseAsync("""{"elements":[]}""")))),
            Options.Create(CreateLinkedInOptions()));

    private static LinkedInOptions CreateLinkedInOptions() => new()
    {
        ClientId = "linkedin-client",
        ClientSecret = "linkedin-secret",
        RedirectUri = "https://app.example.com/api/v1/social/linkedin/callback",
        Scopes = ["openid", "profile", "w_member_social"]
    };

    private static XOptions CreateXOptions() => new()
    {
        ClientId = "x-client",
        ClientSecret = "x-secret",
        RedirectUri = "https://app.example.com/api/v1/social/x/callback",
        Scopes = ["tweet.read", "tweet.write", "users.read", "offline.access"]
    };

    private static HttpResponseMessage JsonResponse(string json) =>
        Status(HttpStatusCode.OK, json);

    private static Task<HttpResponseMessage> JsonResponseAsync(string json) =>
        Task.FromResult(JsonResponse(json));

    private static HttpResponseMessage Status(HttpStatusCode statusCode, string body) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
