using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Api.Application.ContentIdeas;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Application.AI;

public sealed class OpenAiProvider : IAiProvider
{
    private const int MaxAttempts = 2;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan DefaultRateLimitRetryDelay = TimeSpan.FromMilliseconds(350);
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public OpenAiProvider(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<OpenAiProvider> logger,
        IHostEnvironment hostEnvironment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        AiResponseSchema responseSchema,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = CreateRequest(systemPrompt, userPrompt, responseSchema);
            HttpResponseMessage response;
            var startedAt = Stopwatch.GetTimestamp();

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
                    "Transient {Provider} network failure for model {Model}; retrying attempt {Attempt}.",
                    _options.Provider,
                    _options.Model,
                    attempt + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                continue;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    ProviderErrorDetails error;
                    try
                    {
                        error = await ReadProviderErrorAsync(response, cancellationToken);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new AiProviderTimeoutException();
                    }

                    LogProviderDiagnostics(
                        response,
                        error,
                        systemPrompt,
                        userPrompt,
                        Stopwatch.GetElapsedTime(startedAt),
                        attempt);

                    var isQuotaFailure = IsQuotaFailure(error);
                    var isTemporaryRateLimit =
                        response.StatusCode == (HttpStatusCode)429 &&
                        !isQuotaFailure &&
                        IsTemporaryRateLimit(error);
                    var retryDelay = response.StatusCode == (HttpStatusCode)429
                        ? GetRateLimitRetryAfter(response).Delay
                        : GetRetryDelay(response);
                    var shouldRetry = attempt < MaxAttempts &&
                        !isQuotaFailure &&
                        (isTemporaryRateLimit || IsTransientServerFailure(response.StatusCode)) &&
                        retryDelay <= MaxRetryDelay;

                    if (shouldRetry)
                    {
                        _logger.LogWarning(
                            "Transient {Provider} HTTP {StatusCode} for model {Model} after {DurationMilliseconds} ms; " +
                            "retrying attempt {Attempt} after {DelayMilliseconds} ms.",
                            _options.Provider,
                            (int)response.StatusCode,
                            _options.Model,
                            (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                            attempt + 1,
                            (long)retryDelay.TotalMilliseconds);
                        await Task.Delay(retryDelay, cancellationToken);
                        continue;
                    }

                    throw CreateProviderException(response.StatusCode, error);
                }

                try
                {
                    var content = await ReadStructuredContentAsync(response, cancellationToken);
                    _logger.LogInformation(
                        "AI provider {Provider} completed model {Model} request with HTTP {StatusCode} " +
                        "in {DurationMilliseconds} ms on attempt {Attempt}.",
                        _options.Provider,
                        _options.Model,
                        (int)response.StatusCode,
                        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                        attempt);
                    return content;
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
                        "Transient {Provider} response-read failure for model {Model} after {DurationMilliseconds} ms; " +
                        "retrying attempt {Attempt}.",
                        _options.Provider,
                        _options.Model,
                        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                        attempt + 1);
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                    continue;
                }
            }
        }

        throw new AiProviderUnavailableException();
    }

    private HttpRequestMessage CreateRequest(
        string systemPrompt,
        string userPrompt,
        AiResponseSchema responseSchema)
    {
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7,
            max_tokens = 4096,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = responseSchema.Name,
                    strict = true,
                    schema = responseSchema.Schema
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

    private static Exception CreateProviderException(
        HttpStatusCode statusCode,
        ProviderErrorDetails error)
    {
        if (IsQuotaFailure(error) || statusCode == HttpStatusCode.PaymentRequired)
        {
            return new AiProviderQuotaExceededException();
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new AiProviderAuthenticationException();
        }

        if (statusCode == (HttpStatusCode)429)
        {
            return new AiProviderRateLimitedException();
        }

        if (statusCode is HttpStatusCode.RequestTimeout)
        {
            return new AiProviderTimeoutException();
        }

        return IsTransientServerFailure(statusCode)
            ? new AiProviderUnavailableException()
            : new AiProviderRequestException();
    }

    private static async Task<ProviderErrorDetails> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("error", out var error) ||
                error.ValueKind != JsonValueKind.Object)
            {
                return new ProviderErrorDetails();
            }

            return new ProviderErrorDetails(
                GetStringProperty(error, "type"),
                GetStringProperty(error, "code"),
                GetStringProperty(error, "message"));
        }
        catch (JsonException)
        {
            return new ProviderErrorDetails();
        }
    }

    private static string? GetStringProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool IsQuotaFailure(ProviderErrorDetails error)
    {
        var details = string.Join(' ', error.Type, error.Code, error.Message);
        return details.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("billing", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("payment_required", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemporaryRateLimit(ProviderErrorDetails error)
    {
        var details = string.Join(' ', error.Type, error.Code, error.Message);
        return details.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(error.Type, "requests", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(error.Type, "tokens", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientServerFailure(HttpStatusCode statusCode) =>
        (int)statusCode >= (int)HttpStatusCode.InternalServerError;

    private static RateLimitRetryAfter GetRateLimitRetryAfter(
        HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return new RateLimitRetryAfter(true, delta < TimeSpan.Zero ? TimeSpan.Zero : delta);
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return new RateLimitRetryAfter(
                true,
                delay < TimeSpan.Zero ? TimeSpan.Zero : delay);
        }

        return new RateLimitRetryAfter(false, DefaultRateLimitRetryDelay);
    }

    private void LogProviderDiagnostics(
        HttpResponseMessage response,
        ProviderErrorDetails error,
        string systemPrompt,
        string userPrompt,
        TimeSpan duration,
        int attempt)
    {
        _logger.LogWarning(
            "AI provider {Provider} returned HTTP {StatusCode} for model {Model} " +
            "after {DurationMilliseconds} ms on attempt {Attempt}.",
            _options.Provider,
            (int)response.StatusCode,
            SafeProviderValue(_options.Model, systemPrompt, userPrompt),
            (long)duration.TotalMilliseconds,
            attempt);

        if (!_hostEnvironment.IsDevelopment())
        {
            return;
        }

        _logger.LogWarning(
            "{Provider} returned HTTP {StatusCode} for {Method} {Endpoint} using model {Model}. " +
            "Error type {ProviderErrorType}; " +
            "error code {ProviderErrorCode}; message {ProviderMessage}; retry-after milliseconds {RetryAfterMilliseconds}.",
            _options.Provider,
            (int)response.StatusCode,
            "POST",
            "chat/completions",
            SafeProviderValue(_options.Model, systemPrompt, userPrompt),
            SafeProviderValue(error.Type, systemPrompt, userPrompt),
            SafeProviderValue(error.Code, systemPrompt, userPrompt),
            SafeProviderValue(error.Message, systemPrompt, userPrompt),
            GetRetryAfterMilliseconds(response));
    }

    private string? SafeProviderValue(
        string? value,
        string systemPrompt,
        string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if ((!string.IsNullOrEmpty(systemPrompt) &&
                value.Contains(systemPrompt, StringComparison.Ordinal)) ||
            (!string.IsNullOrEmpty(userPrompt) &&
                value.Contains(userPrompt, StringComparison.Ordinal)))
        {
            return null;
        }

        var safeValue = new string(value.Where(character => !char.IsControl(character)).ToArray());
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            safeValue = safeValue.Replace(_options.ApiKey, "[REDACTED]", StringComparison.Ordinal);
        }

        return safeValue.Length <= 512 ? safeValue : safeValue[..512];
    }

    private static long? GetRetryAfterMilliseconds(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ??
            (retryAfter?.Date - DateTimeOffset.UtcNow);
        return delay is null ? null : (long?)Math.Ceiling(Math.Max(0, delay.Value.TotalMilliseconds));
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ??
            (retryAfter?.Date - DateTimeOffset.UtcNow) ??
            TimeSpan.FromMilliseconds(350);

        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private sealed record ProviderErrorDetails(string? Type = null, string? Code = null, string? Message = null);

    private sealed record RateLimitRetryAfter(bool IsPresent, TimeSpan Delay);
}