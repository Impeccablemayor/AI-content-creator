using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.Application.AI;
using Api.Application.ContentIdeas;
using Api.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class OpenAiProviderTests
{
    [Fact]
    public async Task Sends_strict_structured_output_request_and_returns_content()
    {
        const string structuredContent = "{\"ideas\":[]}";
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/chat/completions", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);

            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync());
            var root = body.RootElement;
            Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
            Assert.Equal("system context", root.GetProperty("messages")[0]
                .GetProperty("content").GetString());
            Assert.Equal("user context", root.GetProperty("messages")[1]
                .GetProperty("content").GetString());
            Assert.Equal("json_schema", root.GetProperty("response_format")
                .GetProperty("type").GetString());
            Assert.True(root.GetProperty("response_format")
                .GetProperty("json_schema").GetProperty("strict").GetBoolean());
            Assert.False(root.GetProperty("response_format")
                .GetProperty("json_schema").GetProperty("schema")
                .GetProperty("additionalProperties").GetBoolean());

            return JsonResponse(new { choices = new[] { new { message = new { content = structuredContent } } } });
        });
        var provider = CreateProvider(handler);

        var result = await provider.GenerateAsync("system context", "user context");

        Assert.Equal(structuredContent, result);
    }

    [Fact]
    public async Task Malformed_provider_response_is_rejected()
    {
        var provider = CreateProvider(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{")
            })));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            provider.GenerateAsync("system", "user"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"choices\":[null]}")]
    [InlineData("{\"choices\":[{\"message\":null}]}")]
    public async Task Invalid_response_envelopes_are_rejected(string responseBody)
    {
        var provider = CreateProvider(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            })));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            provider.GenerateAsync("system", "user"));
    }

    [Fact]
    public async Task Invalid_credentials_are_not_retried_or_exposed()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderAuthenticationException>(
            () => provider.GenerateAsync("system", "user"));

        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain("test-key", exception.ToString());
    }

    [Fact]
    public async Task Rate_limit_retries_once_then_returns_safe_provider_error()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            provider.GenerateAsync("system", "user"));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Cancellation_flows_to_http_request()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GenerateAsync("system", "user", cancellation.Token));
    }

    [Fact]
    public async Task Http_timeout_is_mapped_to_safe_provider_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = CreateProvider(handler, TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAsync<AiProviderTimeoutException>(() =>
            provider.GenerateAsync("system", "user"));
    }

    [Fact]
    public async Task Server_failure_is_retried_once_and_mapped_as_unavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            provider.GenerateAsync("system", "user"));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Transient_network_failure_is_retried_once()
    {
        StubHttpMessageHandler handler = null!;
        handler = new StubHttpMessageHandler((_, _) =>
        {
            if (handler.RequestCount == 1)
            {
                throw new HttpRequestException();
            }

            return Task.FromResult(JsonResponse(new
            {
                choices = new[]
                {
                    new { message = new { content = "{\"ideas\":[]}" } }
                }
            }));
        });
        var provider = CreateProvider(handler);

        var result = await provider.GenerateAsync("system", "user");

        Assert.Equal("{\"ideas\":[]}", result);
        Assert.Equal(2, handler.RequestCount);
    }

    private static OpenAiProvider CreateProvider(
        HttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        return new OpenAiProvider(
            client,
            Options.Create(new AiOptions
            {
                Provider = "OpenAI",
                Model = "gpt-4o-mini",
                ApiKey = "test-key",
                TimeoutSeconds = 5
            }),
            NullLogger<OpenAiProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value))
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return send(request, cancellationToken);
        }
    }
}