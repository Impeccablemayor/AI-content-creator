using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Application.ContentIdeas;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Application.AI;

public sealed class OpenAiProvider : IAiProvider
{
    private const int MaxAttempts = 2;
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly object StructuredOutputSchema = new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "ideas" },
        properties = new
        {
            ideas = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "title", "topic", "angle", "hook", "reason", "hashtags", "scores"
                    },
                    properties = new
                    {
                        title = new { type = "string" },
                        topic = new { type = "string" },
                        angle = new { type = "string" },
                        hook = new { type = "string" },
                        reason = new { type = "string" },
                        hashtags = new
                        {
                            type = "array",
                            items = new { type = "string" }
                        },
                        scores = new
                        {
                            type = "object",
                            additionalProperties = false,
                            required = new[]
                            {
                                "relevance", "engagementPotential", "brandFit"
                            },
                            properties = new
                            {
                                relevance = new { type = "number" },
                                engagementPotential = new { type = "number" },
                                brandFit = new { type = "number" }
                            }
                        }
                    }
                }
            }
        }
    };

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = CreateRequest(systemPrompt, userPrompt);
            HttpResponseMessage response;

            try
            {
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiProviderTimeoutException();
            }
            catch (HttpRequestException)
            {
                if (attempt == MaxAttempts)
                {
                    throw new AiProviderUnavailableException();
                }

                _logger.LogWarning(
                    "Transient AI provider network failure; retrying attempt {Attempt}.",
                    attempt + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                continue;
            }

            using (response)
            {
                if (IsTransient(response.StatusCode) && attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        "Transient AI provider response {StatusCode}; retrying attempt {Attempt}.",
                        (int)response.StatusCode,
                        attempt + 1);
                    await Task.Delay(GetRetryDelay(response), cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw CreateProviderException(response.StatusCode);
                }

                try
                {
                    return await ReadStructuredContentAsync(response, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new AiProviderTimeoutException();
                }
                catch (HttpRequestException)
                {
                    if (attempt == MaxAttempts)
                    {
                        throw new AiProviderUnavailableException();
                    }

                    _logger.LogWarning(
                        "Transient AI provider response-read failure; retrying attempt {Attempt}.",
                        attempt + 1);
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                    continue;
                }
            }
        }

        throw new AiProviderUnavailableException();
    }

    private HttpRequestMessage CreateRequest(string systemPrompt, string userPrompt)
    {
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "content_idea_generation",
                    strict = true,
                    schema = StructuredOutputSchema
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return request;
    }

    private static async Task<string> ReadStructuredContentAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var contentStream = await response.Content
                .ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                contentStream,
                cancellationToken: cancellationToken);

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                throw new InvalidAiResponseException(
                    "The AI provider returned no structured content.");
            }

            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object ||
                !choice.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidAiResponseException(
                    "The AI provider returned no structured content.");
            }

            if (message.TryGetProperty("refusal", out var refusal) &&
                refusal.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(refusal.GetString()))
            {
                throw new InvalidAiResponseException(
                    "The AI provider could not fulfill the generation request.");
            }

            if (message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(content.GetString()))
            {
                return content.GetString()!;
            }
        }
        catch (JsonException)
        {
            throw new InvalidAiResponseException(
                "The AI provider returned a malformed response.");
        }

        throw new InvalidAiResponseException(
            "The AI provider returned no structured content.");
    }

    private static Exception CreateProviderException(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new AiProviderAuthenticationException(),
            (HttpStatusCode)429 => new AiProviderRateLimitedException(),
            (HttpStatusCode)408 => new AiProviderTimeoutException(),
            >= HttpStatusCode.InternalServerError =>
                new AiProviderUnavailableException(),
            _ => new AiProviderRequestException()
        };

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == (HttpStatusCode)429 ||
        (int)statusCode >= (int)HttpStatusCode.InternalServerError;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ??
            (retryAfter?.Date - DateTimeOffset.UtcNow) ??
            TimeSpan.FromMilliseconds(350);

        return TimeSpan.FromMilliseconds(Math.Clamp(delay.TotalMilliseconds, 0, 1500));
    }
}