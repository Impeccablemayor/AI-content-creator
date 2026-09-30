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
            Assert.Equal(0.7, root.GetProperty("temperature").GetDouble());
            Assert.Equal(4096, root.GetProperty("max_tokens").GetInt32());
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

        var result = await GenerateAsync(
            provider,
            "system context",
            "user context",
            ContentIdeaResponseSchema.Value);

        Assert.Equal(structuredContent, result);
    }

    [Fact]
    public async Task Caller_supplied_schema_name_and_body_are_sent()
    {
        const string structuredContent = """
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
            var jsonSchema = body.RootElement.GetProperty("response_format")
                .GetProperty("json_schema");

            Assert.Equal(ContentPostResponseSchema.Value.Name, jsonSchema.GetProperty("name").GetString());
            Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
            var schema = jsonSchema.GetProperty("schema");
            var properties = schema.GetProperty("properties");
            var propertyNames = properties.EnumerateObject()
                .Select(property => property.Name).ToArray();
            Assert.Equal("string", properties.GetProperty("content").GetProperty("type").GetString());
            Assert.Contains(
                "Educational",
                properties.GetProperty("contentType").GetProperty("enum")
                    .EnumerateArray().Select(item => item.GetString()!).ToArray());
            Assert.Contains("content", propertyNames);
            Assert.DoesNotContain("ideas", propertyNames);

            return JsonResponse(new { choices = new[] { new { message = new { content = structuredContent } } } });
        });
        var provider = CreateProvider(handler);

        var result = await GenerateAsync(
            provider,
            "post system prompt",
            "post user prompt",
            ContentPostResponseSchema.Value);

        Assert.Equal(structuredContent, result);
    }

    [Fact]
    public async Task Content_post_requests_do_not_send_the_content_idea_schema_name()
    {
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var jsonSchema = body.RootElement.GetProperty("response_format")
                .GetProperty("json_schema");

            Assert.NotEqual("content_idea_generation", jsonSchema.GetProperty("name").GetString());
            Assert.Equal(ContentPostResponseSchema.Value.Name, jsonSchema.GetProperty("name").GetString());
            Assert.DoesNotContain(
                "ideas",
                jsonSchema.GetProperty("schema").GetProperty("properties").EnumerateObject()
                    .Select(property => property.Name).ToArray());

            return JsonResponse(new
            {
                choices = new[] { new { message = new { content = "{\"platform\":\"X\"}" } } }
            });
        });
        var provider = CreateProvider(handler);

        var result = await GenerateAsync(provider, responseSchema: ContentPostResponseSchema.Value);

        Assert.Equal("{\"platform\":\"X\"}", result);
    }

    [Theory]
    [InlineData("Groq", "https://api.groq.com/openai/v1/", "openai/gpt-oss-20b")]
    [InlineData("OpenRouter", "https://openrouter.ai/api/v1/", "provider/model-id")]
    public async Task Openai_compatible_provider_sends_configured_model_and_parses_structured_response(
        string providerName,
        string baseUrl,
        string model)
    {
        const string structuredContent = "{\"ideas\":[]}";
        var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            Assert.Equal(new Uri(baseUrl + "chat/completions"), request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);

            using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            Assert.Equal(model, body.RootElement.GetProperty("model").GetString());

            return JsonResponse(new
            {
                choices = new[] { new { message = new { content = structuredContent } } }
            });
        });
        var logger = new CapturingLogger<OpenAiProvider>();
        var provider = CreateProvider(
            handler,
            logger: logger,
            providerName: providerName,
            baseUrl: baseUrl,
            model: model);

        var result = await GenerateAsync(
            provider,
            "safe system prompt",
            "safe user prompt");

        Assert.Equal(structuredContent, result);
        Assert.Contains(logger.Messages, message =>
            message.Contains(providerName, StringComparison.Ordinal) &&
            message.Contains(model, StringComparison.Ordinal) &&
            message.Contains("HTTP 200", StringComparison.Ordinal) &&
            message.Contains("attempt 1", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("safe system prompt", StringComparison.Ordinal) ||
            message.Contains("safe user prompt", StringComparison.Ordinal) ||
            message.Contains("test-key", StringComparison.Ordinal));
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
            GenerateAsync(provider));
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
            GenerateAsync(provider));
    }

    [Fact]
    public async Task Invalid_credentials_are_not_retried_or_exposed()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<AiProviderAuthenticationException>(
            () => GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain("test-key", exception.ToString());
    }

    [Fact]
    public async Task Rate_limit_retries_once_then_returns_safe_provider_error()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonErrorResponse(
                (HttpStatusCode)429,
                "requests",
                "rate_limit_exceeded",
                "Rate limit reached for gpt-4o-mini.")));
        var logger = new CapturingLogger<OpenAiProvider>();
        var provider = CreateProvider(handler, logger: logger);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains(logger.Messages, message =>
            message.Contains("HTTP 429", StringComparison.Ordinal) &&
            message.Contains("OpenAI", StringComparison.Ordinal) &&
            message.Contains("rate_limit_exceeded", StringComparison.Ordinal) &&
            message.Contains("gpt-4o-mini", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("test-key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rate_limit_respects_retry_after_header()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = JsonErrorResponse(
                (HttpStatusCode)429,
                "tokens",
                "rate_limit_exceeded",
                "Rate limit reached.");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        });
        var logger = new CapturingLogger<OpenAiProvider>();
        var provider = CreateProvider(handler, logger: logger);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains(logger.Messages, message =>
            message.Contains("retry-after milliseconds 1000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rate_limit_does_not_retry_after_a_delay_beyond_the_bound()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = JsonErrorResponse(
                (HttpStatusCode)429,
                "requests",
                "rate_limit_exceeded",
                "Rate limit reached.");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
            return Task.FromResult(response);
        });
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("insufficient_quota", "insufficient_quota", "You exceeded your current quota; check billing details.")]
    [InlineData("billing_error", "billing_hard_limit_reached", "Billing limit reached.")]
    public async Task Quota_or_billing_rate_limit_is_not_retried_or_exposed(
        string errorType,
        string errorCode,
        string errorMessage)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonErrorResponse(
                (HttpStatusCode)429,
                errorType,
                errorCode,
                errorMessage)));
        var logger = new CapturingLogger<OpenAiProvider>();
        var provider = CreateProvider(handler, logger: logger);

        var exception = await Assert.ThrowsAsync<AiProviderQuotaExceededException>(() =>
            GenerateAsync(provider, "system", "sensitive request payload"));

        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain("test-key", exception.ToString());
        Assert.Contains(logger.Messages, message =>
            message.Contains(errorCode, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("test-key", StringComparison.Ordinal) ||
            message.Contains("sensitive request payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Payment_required_maps_to_quota_error_without_retry()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonErrorResponse(
                HttpStatusCode.PaymentRequired,
                "billing_error",
                "payment_required",
                "Provider credits are exhausted.")));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderQuotaExceededException>(() =>
            GenerateAsync(provider));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Provider_message_that_echoes_request_payload_is_not_logged()
    {
        const string userPrompt = "sensitive request payload";
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonErrorResponse(
                (HttpStatusCode)429,
                "requests",
                "rate_limit_exceeded",
                $"Rate limit reached for {userPrompt}.")));
        var logger = new CapturingLogger<OpenAiProvider>();
        var provider = CreateProvider(handler, logger: logger);

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider, "system context", userPrompt));

        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains(userPrompt, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rate_limit_diagnostics_are_not_logged_outside_development()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(JsonErrorResponse(
                (HttpStatusCode)429,
                "requests",
                "rate_limit_exceeded",
                "Rate limit reached.")));
        var logger = new CapturingLogger<OpenAiProvider>();
        var provider = CreateProvider(handler, logger: logger, environmentName: "Production");

        await Assert.ThrowsAsync<AiProviderRateLimitedException>(() =>
            GenerateAsync(provider));

        Assert.Contains(logger.Messages, message =>
            message.Contains("AI provider OpenAI", StringComparison.Ordinal) &&
            message.Contains("HTTP 429", StringComparison.Ordinal) &&
            message.Contains("gpt-4o-mini", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("Provider limit category", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("Rate limit reached", StringComparison.Ordinal));
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
            GenerateAsync(provider, cancellationToken: cancellation.Token));
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
            GenerateAsync(provider));
    }

    [Fact]
    public async Task Server_failure_is_retried_once_and_mapped_as_unavailable()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            GenerateAsync(provider));

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

        var result = await GenerateAsync(provider);

        Assert.Equal("{\"ideas\":[]}", result);
        Assert.Equal(2, handler.RequestCount);
    }

    private static Task<string> GenerateAsync(
        OpenAiProvider provider,
        string systemPrompt = "system",
        string userPrompt = "user",
        AiResponseSchema? responseSchema = null,
        CancellationToken cancellationToken = default) =>
        provider.GenerateAsync(
            systemPrompt,
            userPrompt,
            responseSchema ?? ContentIdeaResponseSchema.Value,
            cancellationToken);

    private static OpenAiProvider CreateProvider(
        HttpMessageHandler handler,
        TimeSpan? timeout = null,
        ILogger<OpenAiProvider>? logger = null,
        string environmentName = "Development",
        string providerName = "OpenAI",
        string baseUrl = "https://api.openai.com/v1/",
        string model = "gpt-4o-mini")
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        return new OpenAiProvider(
            client,
            Options.Create(new AiOptions
            {
                Provider = providerName,
                Model = model,
                BaseUrl = baseUrl,
                ApiKey = "test-key",
                TimeoutSeconds = 5
            }),
            logger ?? NullLogger<OpenAiProvider>.Instance,
            new TestHostEnvironment(environmentName));
    }

    private static HttpResponseMessage JsonErrorResponse(
        HttpStatusCode statusCode,
        string type,
        string code,
        string message) => new(statusCode)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            error = new { type, code, message }
        }))
    };

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