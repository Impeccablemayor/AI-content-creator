using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.Application.AI;
using Api.Application.ContentIdeas;
using Api.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class GeminiProviderTests
{
    [Fact]
    public async Task Sends_native_structured_output_request_and_returns_json()
    {
        const string structuredJson = "{\"ideas\":[]}";
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "/v1beta/models/gemini-test-model:generateContent",
                request.RequestUri!.AbsolutePath);
            Assert.Equal("test-placeholder", request.Headers.GetValues("x-goog-api-key").Single());
            Assert.DoesNotContain("test-placeholder", request.RequestUri.ToString());

            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root = body.RootElement;
            Assert.Equal("shared system prompt", root.GetProperty("systemInstruction")
                .GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("shared user prompt", root.GetProperty("contents")[0]
                .GetProperty("parts")[0].GetProperty("text").GetString());
            var responseFormat = root.GetProperty("generationConfig")
                .GetProperty("responseFormat").GetProperty("text");
            Assert.Equal("APPLICATION_JSON", responseFormat.GetProperty("mimeType").GetString());
            Assert.Equal("array", responseFormat.GetProperty("schema")
                .GetProperty("properties").GetProperty("ideas").GetProperty("type").GetString());

            return JsonResponse(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = structuredJson } } } }
                }
            });
        });
        var provider = CreateProvider(handler);

        var result = await GenerateAsync(
            provider,
            "shared system prompt",
            "shared user prompt",
            ContentIdeaResponseSchema.Value);

        Assert.Equal(structuredJson, result);
    }

    [Fact]
    public async Task Content_post_schema_is_sent_and_post_shaped_response_is_returned()
    {
        const string structuredJson = """
            {
              "platform": "LinkedIn",
              "content": "Example post content.",
              "hook": "Example hook.",
              "callToAction": "What do you think?",
              "hashtags": ["#SoftwareEngineering"],
              "contentType": "Educational",
              "storytellingStructure": "Hook → explanation → takeaway → CTA"
            }
            """;
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var schema = body.RootElement.GetProperty("generationConfig")
                .GetProperty("responseFormat").GetProperty("text")
                .GetProperty("schema");

            Assert.Equal("application/json", request.Content.Headers.ContentType!.MediaType);
            Assert.Equal("object", schema.GetProperty("type").GetString());
            var properties = schema.GetProperty("properties");
            var propertyNames = properties.EnumerateObject()
                .Select(property => property.Name).ToArray();
            Assert.Equal("string", properties.GetProperty("content").GetProperty("type").GetString());
            Assert.Equal("string", properties.GetProperty("hook").GetProperty("type").GetString());
            Assert.Equal("array", properties.GetProperty("hashtags").GetProperty("type").GetString());
            Assert.Contains(
                "LinkedIn",
                properties.GetProperty("platform").GetProperty("enum")
                    .EnumerateArray().Select(item => item.GetString()!).ToArray());
            Assert.Contains("content", propertyNames);
            Assert.DoesNotContain("ideas", propertyNames);
            Assert.DoesNotContain(
                "ideas",
                schema.GetProperty("required").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray());

            return JsonResponse(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = structuredJson } } } }
                }
            });
        });
        var provider = CreateProvider(handler);

        var result = await GenerateAsync(
            provider,
            "post system prompt",
            "post user prompt",
            ContentPostResponseSchema.Value);

        Assert.Equal(structuredJson, result);
    }

    [Fact]
    public async Task Content_idea_schema_is_not_used_for_content_post_requests()
    {
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var schema = body.RootElement.GetProperty("generationConfig")
                .GetProperty("responseFormat").GetProperty("text")
                .GetProperty("schema");

            var propertyNames = schema.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name).ToArray();
            Assert.Contains("content", propertyNames);
            Assert.DoesNotContain("ideas", propertyNames);

            return JsonResponse(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = "{\"platform\":\"X\"}" } } } }
                }
            });
        });
        var provider = CreateProvider(handler);

        var result = await GenerateAsync(
            provider,
            responseSchema: ContentPostResponseSchema.Value);

        Assert.Equal("{\"platform\":\"X\"}", result);
    }

    [Fact]
    public async Task Malformed_structured_json_is_rejected()
    {
        var provider = CreateProvider(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = "{" } } } }
                }
            }))));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            GenerateAsync(provider));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"plain text\"")]
    [InlineData("42")]
    [InlineData("null")]
    public async Task Non_object_structured_json_is_rejected(string generatedText)
    {
        var provider = CreateProvider(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonResponse(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = generatedText } } } }
                }
            }))));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            GenerateAsync(provider));
    }

    [Fact]
    public async Task Temporary_429_retries_once_and_maps_to_safe_rate_limit()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            (HttpStatusCode)429,
            "RESOURCE_EXHAUSTED",
            "Too many requests due to rate limit; please try again.")));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData((int)HttpStatusCode.TooManyRequests)]
    [InlineData((int)HttpStatusCode.Forbidden)]
    public async Task Quota_exhaustion_is_not_retried_and_maps_to_quota_error(int statusCode)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            (HttpStatusCode)statusCode,
            "RESOURCE_EXHAUSTED",
            "You exceeded the quota; check billing and credits.",
            new[]
            {
                new Dictionary<string, string>
                {
                    ["@type"] = "type.googleapis.com/google.rpc.QuotaFailure"
                }
            })));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderQuotaExceededException>(() =>
            GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Retry_info_is_respected_for_temporary_429()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            (HttpStatusCode)429,
            "RESOURCE_EXHAUSTED",
            "Temporary capacity issue.",
            new[]
            {
                new Dictionary<string, string>
                {
                    ["@type"] = "type.googleapis.com/google.rpc.RetryInfo",
                    ["retryDelay"] = "0s"
                }
            })));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Retry_after_header_is_respected_for_generic_429()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = ErrorResponse(
                (HttpStatusCode)429,
                "RESOURCE_EXHAUSTED",
                "Temporary resource exhaustion.");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        });
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Retry_info_beyond_the_delay_bound_does_not_retry()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            (HttpStatusCode)429,
            "RESOURCE_EXHAUSTED",
            "Temporary rate limit.",
            new[]
            {
                new Dictionary<string, string>
                {
                    ["@type"] = "type.googleapis.com/google.rpc.RetryInfo",
                    ["retryDelay"] = "5s"
                }
            })));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Invalid_api_key_maps_to_authentication_error_without_retry()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.Unauthorized,
            "UNAUTHENTICATED",
            "Invalid API key.")));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderAuthenticationException>(() =>
            GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Invalid_request_maps_without_retry()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.BadRequest,
            "INVALID_ARGUMENT",
            "Invalid generation configuration.")));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRequestException>(() =>
            GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Not_found_logs_safe_provider_diagnostics_in_development()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.NotFound,
            "NOT_FOUND",
            "models/gemini-test-model is not found for generateContent.",
            code: 404)));
        var logger = new CapturingLogger<GeminiProvider>();
        var provider = CreateProvider(handler, logger: logger);

        await Assert.ThrowsAsync<AiProviderRequestException>(() =>
            GenerateAsync(
                provider,
                "request system prompt",
                "request user prompt"));

        Assert.Single(logger.Messages);
        Assert.Contains("HTTP 404", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("NOT_FOUND", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("404", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("gemini-test-model", logger.Messages[0], StringComparison.Ordinal);
        Assert.Contains("is not found", logger.Messages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("test-placeholder", logger.Messages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("request user prompt", logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_error_echoing_prompt_or_key_is_omitted_from_diagnostics()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.NotFound,
            "NOT_FOUND",
            "Sensitive request user prompt with test-placeholder.",
            code: 404)));
        var logger = new CapturingLogger<GeminiProvider>();
        var provider = CreateProvider(handler, logger: logger);

        await Assert.ThrowsAsync<AiProviderRequestException>(() =>
            GenerateAsync(
                provider,
                "system prompt",
                "Sensitive request user prompt"));

        Assert.Single(logger.Messages);
        Assert.DoesNotContain("Sensitive request user prompt", logger.Messages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("test-placeholder", logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_error_details_are_not_logged_outside_development()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.NotFound,
            "NOT_FOUND",
            "models/gemini-test-model is not found.",
            code: 404)));
        var logger = new CapturingLogger<GeminiProvider>();
        var provider = CreateProvider(handler, logger: logger, environmentName: "Production");

        await Assert.ThrowsAsync<AiProviderRequestException>(() =>
            GenerateAsync(provider));

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task Server_failure_retries_once_then_maps_unavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            GenerateAsync(provider));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Transient_network_failure_retries_once()
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
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[] { new { text = "{\"ideas\":[]}" } }
                        }
                    }
                }
            }));
        });
        var provider = CreateProvider(handler);

        var result = await GenerateAsync(provider);

        Assert.Equal("{\"ideas\":[]}", result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Http_timeout_maps_to_provider_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provider = CreateProvider(handler, TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAsync<AiProviderTimeoutException>(() =>
            GenerateAsync(provider));
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
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
            GenerateAsync(provider, cancellationToken: cancellation.Token));
    }

    private static Task<string> GenerateAsync(
        GeminiProvider provider,
        string systemPrompt = "system",
        string userPrompt = "user",
        AiResponseSchema? responseSchema = null,
        CancellationToken cancellationToken = default) =>
        provider.GenerateAsync(
            systemPrompt,
            userPrompt,
            responseSchema ?? ContentIdeaResponseSchema.Value,
            cancellationToken);

    private static GeminiProvider CreateProvider(
        HttpMessageHandler handler,
        TimeSpan? timeout = null,
        ILogger<GeminiProvider>? logger = null,
        string environmentName = "Development")
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        return new GeminiProvider(
            client,
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "gemini-test-model",
                ApiKey = "test-placeholder",
                TimeoutSeconds = 5
            }),
            logger ?? NullLogger<GeminiProvider>.Instance,
            new TestHostEnvironment(environmentName));
    }

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value))
    };

    private static HttpResponseMessage ErrorResponse(
        HttpStatusCode statusCode,
        string status,
        string message,
        object[]? details = null,
        int? code = null) => new(statusCode)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            error = new { code, status, message, details }
        }))
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}