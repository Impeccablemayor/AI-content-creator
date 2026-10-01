using System.Net;
using System.Text;
using System.Text.Json;
using Api.Application.Publishing;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class SocialPlatformPublisherTests
{
    private const string AccessToken = "publish-access-token";
    private const string PlatformAccountId = "abc123";

    [Fact]
    public async Task LinkedIn_publishes_through_the_ugc_posts_endpoint_with_the_stored_account_urn()
    {
        // The body is read inside the handler because the publisher disposes the
        // request once the call returns.
        Uri? uri = null;
        string? authorization = null;
        string? body = null;
        var publisher = CreateLinkedInPublisher(async (request, _) =>
        {
            uri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"id":"urn:li:share:42"}""");
        });

        var result = await publisher.PublishAsync(
            AccessToken,
            new SocialPublishRequest("Body text", PlatformAccountId),
            CancellationToken.None);

        Assert.Equal("urn:li:share:42", result.ExternalPostId);
        Assert.Equal("https://api.linkedin.com/v2/ugcPosts", uri?.ToString());
        Assert.Equal($"Bearer {AccessToken}", authorization);

        using var document = JsonDocument.Parse(Assert.IsType<string>(body));
        var root = document.RootElement;
        Assert.Equal($"urn:li:person:{PlatformAccountId}", root.GetProperty("author").GetString());
        Assert.Equal("PUBLISHED", root.GetProperty("lifecycleState").GetString());
        Assert.Equal(
            "Body text",
            root
                .GetProperty("specificContent")
                .GetProperty("com.linkedin.ugc.ShareContent")
                .GetProperty("shareCommentary")
                .GetProperty("text")
                .GetString());
    }

    [Fact]
    public async Task X_publishes_the_text_and_returns_a_status_url()
    {
        Uri? uri = null;
        string? body = null;
        var publisher = CreateXPublisher(async (request, _) =>
        {
            uri = request.RequestUri;
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"data":{"id":"1900","text":"Body text"}}""");
        });

        var result = await publisher.PublishAsync(
            AccessToken,
            new SocialPublishRequest("Body text", PlatformAccountId),
            CancellationToken.None);

        Assert.Equal("1900", result.ExternalPostId);
        Assert.Equal("https://x.com/i/web/status/1900", result.Url);
        Assert.Equal("https://api.twitter.com/2/tweets", uri?.ToString());

        using var document = JsonDocument.Parse(Assert.IsType<string>(body));
        Assert.Equal("Body text", document.RootElement.GetProperty("text").GetString());
    }

    /// <summary>
    /// The access token belongs in the Authorization header only. A token in the
    /// body would end up in provider-side logs and in any request tracing.
    /// </summary>
    [Fact]
    public async Task Neither_publisher_ever_places_the_access_token_in_the_request_body()
    {
        var linkedInBody = await CaptureBodyAsync(
            CreateLinkedInPublisher(),
            """{"id":"1"}""");
        var xBody = await CaptureBodyAsync(
            CreateXPublisher(),
            """{"data":{"id":"1"}}""");

        Assert.DoesNotContain(AccessToken, linkedInBody, StringComparison.Ordinal);
        Assert.DoesNotContain(AccessToken, xBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_containing_quotes_and_newlines_is_escaped_rather_than_corrupting_the_body()
    {
        var body = await CaptureBodyAsync(
            CreateXPublisher(),
            """{"data":{"id":"1"}}""",
            "He said \"hi\"\nsecond line");

        using var document = JsonDocument.Parse(body);
        Assert.Equal("He said \"hi\"\nsecond line", document.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task A_post_with_no_text_is_rejected_before_any_request_is_sent()
    {
        var sent = false;
        var publisher = CreateXPublisher((_, _) =>
        {
            sent = true;
            return JsonResponseAsync("""{"data":{"id":"1"}}""");
        });

        var exception = await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            publisher.PublishAsync(
                AccessToken,
                new SocialPublishRequest("   ", PlatformAccountId),
                CancellationToken.None));

        Assert.False(sent);
        Assert.Contains("no publishable text", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Rejected_credentials_raise_an_authentication_failure(HttpStatusCode statusCode)
    {
        var publisher = CreateXPublisher((_, _) => Task.FromResult(
            Status(statusCode, "{\"error\":\"nope\"}")));

        await Assert.ThrowsAsync<SocialProviderAuthenticationException>(() =>
            publisher.PublishAsync(AccessToken, Request(), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Transient_provider_failures_are_reported_as_unavailable(HttpStatusCode statusCode)
    {
        var publisher = CreateXPublisher((_, _) => Task.FromResult(Status(statusCode, "upstream is down")));

        await Assert.ThrowsAsync<SocialProviderUnavailableException>(() =>
            publisher.PublishAsync(AccessToken, Request(), CancellationToken.None));
    }

    [Fact]
    public async Task A_client_error_is_reported_as_a_rejected_request()
    {
        var publisher = CreateLinkedInPublisher((_, _) =>
            Task.FromResult(Status(HttpStatusCode.BadRequest, """{"message":"text too long"}""")));

        await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            publisher.PublishAsync(AccessToken, Request(), CancellationToken.None));
    }

    /// <summary>
    /// A provider error body can echo the submitted content and, on some
    /// platforms, token material. It must never reach an exception message that
    /// gets logged.
    /// </summary>
    [Fact]
    public async Task Failure_messages_never_carry_the_provider_response_body()
    {
        const string secret = "sensitive-content-echoed-by-provider";
        var publisher = CreateLinkedInPublisher((_, _) =>
            Task.FromResult(Status(
                HttpStatusCode.BadRequest,
                $$"""{"message":"{{secret}}","access_token":"{{AccessToken}}"}""")));

        var exception = await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            publisher.PublishAsync(AccessToken, Request(), CancellationToken.None));

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(AccessToken, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_success_response_without_an_identifier_is_treated_as_invalid()
    {
        var publisher = CreateXPublisher((_, _) => JsonResponseAsync("""{"data":{}}"""));

        var exception = await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            publisher.PublishAsync(AccessToken, Request(), CancellationToken.None));

        Assert.Contains("identifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_success_body_that_is_not_json_is_treated_as_invalid()
    {
        var publisher = CreateXPublisher((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "text/html")
            }));

        await Assert.ThrowsAsync<SocialProviderRequestException>(() =>
            publisher.PublishAsync(AccessToken, Request(), CancellationToken.None));
    }

    [Fact]
    public async Task A_configured_platform_reports_itself_configured_for_publishing()
    {
        Assert.True(CreateLinkedInPublisher().IsConfigured);
        Assert.True(CreateXPublisher().IsConfigured);
        Assert.Equal(ContentPlatform.LinkedIn, CreateLinkedInPublisher().Platform);
        Assert.Equal(ContentPlatform.X, CreateXPublisher().Platform);
    }

    [Fact]
    public void A_platform_without_client_configuration_cannot_publish()
    {
        var options = CreateLinkedInOptions();
        options.ClientSecret = string.Empty;

        var publisher = new LinkedInSocialPlatformPublisher(
            new HttpClient(new StubHttpMessageHandler(
                (_, _) => JsonResponseAsync("""{"id":"1"}"""))),
            Options.Create(options));

        Assert.False(publisher.IsConfigured);
    }

    [Fact]
    public void The_resolver_returns_the_publisher_for_a_supported_platform()
    {
        ISocialPlatformPublisher[] publishers =
        [
            CreateLinkedInPublisher(),
            CreateXPublisher()
        ];
        var resolver = new SocialPlatformPublisherResolver(publishers);

        Assert.Equal(ContentPlatform.LinkedIn, resolver.Get(ContentPlatform.LinkedIn).Platform);
        Assert.Equal(ContentPlatform.X, resolver.Get(ContentPlatform.X).Platform);
    }

    [Fact]
    public void The_resolver_rejects_a_platform_it_cannot_publish()
    {
        var resolver = new SocialPlatformPublisherResolver([CreateLinkedInPublisher()]);

        Assert.Throws<UnsupportedSocialPlatformException>(() => resolver.Get(ContentPlatform.X));
    }

    private static SocialPublishRequest Request() => new("Body text", PlatformAccountId);

    private static async Task<string> CaptureBodyAsync(
        ISocialPlatformPublisher publisher,
        string successBody,
        string text = "Body text")
    {
        string? captured = null;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            captured = await request.Content!.ReadAsStringAsync();
            return JsonResponse(successBody);
        });

        var withHandler = publisher switch
        {
            LinkedInSocialPlatformPublisher => (ISocialPlatformPublisher)new LinkedInSocialPlatformPublisher(
                new HttpClient(handler),
                Options.Create(CreateLinkedInOptions())),
            XSocialPlatformPublisher => new XSocialPlatformPublisher(
                new HttpClient(handler),
                Options.Create(CreateXOptions())),
            _ => throw new InvalidOperationException("Unexpected publisher.")
        };

        await withHandler.PublishAsync(AccessToken, new SocialPublishRequest(text, PlatformAccountId), CancellationToken.None);

        return Assert.IsType<string>(captured);
    }

    private static LinkedInSocialPlatformPublisher CreateLinkedInPublisher(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null) =>
        new(
            new HttpClient(new StubHttpMessageHandler(
                handler ?? ((_, _) => JsonResponseAsync("""{"id":"urn:li:share:1"}""")))),
            Options.Create(CreateLinkedInOptions()));

    private static XSocialPlatformPublisher CreateXPublisher(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null) =>
        new(
            new HttpClient(new StubHttpMessageHandler(
                handler ?? ((_, _) => JsonResponseAsync("""{"data":{"id":"1"}}""")))),
            Options.Create(CreateXOptions()));

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
