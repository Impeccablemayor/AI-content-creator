using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Api.Application.ContentIdeas;
using Api.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Application.AI;

public sealed class GeminiProvider : IAiProvider
{
    private const int MaxAttempts = 2;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan NetworkRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiProvider> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public GeminiProvider(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiProvider> logger,
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
                    "Transient Gemini network failure; retrying attempt {Attempt}.",
                    attempt + 1);
                await Task.Delay(NetworkRetryDelay, cancellationToken);
                continue;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    GeminiError error;
                    try
                    {
                        error = await ReadErrorAsync(response, cancellationToken);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new AiProviderTimeoutException();
                    }

                    LogProviderError(response, error, systemPrompt, userPrompt);
                    var retryDelay = GetRetryDelay(response, error);
                    if (attempt < MaxAttempts &&
                        IsRetryable(response, error) &&
                        retryDelay <= MaxRetryDelay)
                    {
                        _logger.LogWarning(
                            "Transient Gemini HTTP {StatusCode}; retrying attempt {Attempt} after {DelayMilliseconds} ms.",
                            (int)response.StatusCode,
                            attempt + 1,
                            (long)retryDelay.TotalMilliseconds);
                        await Task.Delay(retryDelay, cancellationToken);
                        continue;
                    }

                    throw CreateProviderException(response.StatusCode, error);
                }

                string responseText;
                try
                {
                    responseText = await ReadResponseTextAsync(response, cancellationToken);
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
                        "Transient Gemini response-read failure; retrying attempt {Attempt}.",
                        attempt + 1);
                    await Task.Delay(NetworkRetryDelay, cancellationToken);
                    continue;
                }

                return ValidateStructuredJsonObject(responseText);
            }
        }

        throw new AiProviderUnavailableException();
    }

    private HttpRequestMessage CreateRequest(
        string systemPrompt,
        string userPrompt,
        AiResponseSchema responseSchema)
    {
        var model = _options.Model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? _options.Model["models/".Length..]
            : _options.Model;
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    systemInstruction = new
                    {
                        parts = new[] { new { text = systemPrompt } }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = userPrompt } }
                        }
                    },
                    generationConfig = new
                    {
                        responseFormat = new
                        {
                            text = new
                            {
                                mimeType = "APPLICATION_JSON",
                                schema = responseSchema.Schema
                            }
                        }
                    }
                }, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("x-goog-api-key", _options.ApiKey);
        return request;
    }

    private static async Task<GeminiError> ReadErrorAsync(
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

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("error", out var error) ||
                error.ValueKind != JsonValueKind.Object)
            {
                return new GeminiError();
            }

            var details = error.TryGetProperty("details", out var detailsElement) &&
                detailsElement.ValueKind == JsonValueKind.Array
                ? detailsElement.EnumerateArray().ToArray()
                : [];
            var hasQuotaFailure = false;
            var hasRateLimitReason = false;
            TimeSpan? retryDelay = null;

            foreach (var detail in details)
            {
                if (detail.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var detailType = GetStringProperty(detail, "@type");
                if (detailType?.EndsWith("QuotaFailure", StringComparison.Ordinal) == true)
                {
                    hasQuotaFailure = true;
                }

                if (detailType?.EndsWith("RetryInfo", StringComparison.Ordinal) == true &&
                    detail.TryGetProperty("retryDelay", out var retryDelayElement) &&
                    retryDelayElement.ValueKind == JsonValueKind.String)
                {
                    retryDelay = ParseGoogleDuration(retryDelayElement.GetString());
                }

                if (detail.TryGetProperty("metadata", out var metadata))
                {
                    var reason = GetStringProperty(metadata, "reason");
                    if (ContainsQuotaTerm(reason))
                    {
                        hasQuotaFailure = true;
                    }

                    if (reason?.Contains("RATE_LIMIT", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        hasRateLimitReason = true;
                    }
                }
            }

            return new GeminiError(
                GetIntProperty(error, "code"),
                GetStringProperty(error, "status"),
                GetStringProperty(error, "message"),
                hasQuotaFailure,
                retryDelay,
                hasRateLimitReason);
        }
        catch (JsonException)
        {
            return new GeminiError();
        }
    }

    private static async Task<string> ReadResponseTextAsync(
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
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].ValueKind == JsonValueKind.Object &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Object &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                var text = string.Concat(parts.EnumerateArray()
                    .Where(part => part.ValueKind == JsonValueKind.Object)
                    .Select(part => GetStringProperty(part, "text"))
                    .Where(partText => !string.IsNullOrWhiteSpace(partText)));

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
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

    private static string ValidateStructuredJsonObject(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return responseText;
            }
        }
        catch (JsonException)
        {
            throw new InvalidAiResponseException(
                "The AI response was not valid structured JSON.");
        }

        throw new InvalidAiResponseException(
            "The AI response did not match the expected structured format.");
    }

    private static bool IsRetryable(HttpResponseMessage response, GeminiError error)
    {
        if (response.StatusCode == (HttpStatusCode)429)
        {
            return !IsQuotaExhausted(error) &&
                (IsTemporaryRateLimit(error) || response.Headers.RetryAfter is not null);
        }

        return (int)response.StatusCode >= (int)HttpStatusCode.InternalServerError;
    }

    private void LogProviderError(
        HttpResponseMessage response,
        GeminiError error,
        string systemPrompt,
        string userPrompt)
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            return;
        }

        var retryAfter = response.Headers.RetryAfter?.Delta?.TotalMilliseconds;
        _logger.LogWarning(
            "Gemini returned HTTP {HttpStatusCode} for {Method} {Endpoint} using model {Model}. " +
            "Provider error code {ProviderErrorCode}; status {ProviderStatus}; message {ProviderMessage}; " +
            "retry-after milliseconds {RetryAfterMilliseconds}.",
            (int)response.StatusCode,
            "POST",
            "generateContent",
            SafeProviderValue(_options.Model, systemPrompt, userPrompt),
            error.Code,
            SafeProviderValue(error.Status, systemPrompt, userPrompt),
            SafeProviderValue(error.Message, systemPrompt, userPrompt),
            retryAfter is null ? null : (long?)Math.Ceiling(retryAfter.Value));
    }

    private string? SafeProviderValue(
        string? value,
        string systemPrompt,
        string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            (!string.IsNullOrEmpty(systemPrompt) &&
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

    private static bool IsTemporaryRateLimit(GeminiError error)
    {
        var message = error.Message ?? string.Empty;
        return error.RetryDelay.HasValue || error.HasRateLimitReason ||
            message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("try again", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("temporarily", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuotaExhausted(GeminiError error) =>
        error.HasQuotaFailure ||
        ContainsQuotaTerm(error.Status) ||
        ContainsQuotaTerm(error.Message);

    private static bool ContainsQuotaTerm(string? value) =>
        value?.Contains("quota", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains("billing", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true;

    private static TimeSpan GetRetryDelay(
        HttpResponseMessage response,
        GeminiError error)
    {
        var retryAfter = response.Headers.RetryAfter;
        TimeSpan delay;
        if (retryAfter?.Delta is { } delta)
        {
            delay = delta;
        }
        else if (retryAfter?.Date is { } date)
        {
            delay = date - DateTimeOffset.UtcNow;
        }
        else
        {
            delay = error.RetryDelay ?? DefaultRetryDelay;
        }

        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private static Exception CreateProviderException(
        HttpStatusCode statusCode,
        GeminiError error)
    {
        if (statusCode == HttpStatusCode.Forbidden && IsQuotaExhausted(error))
        {
            return new AiProviderQuotaExceededException();
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new AiProviderAuthenticationException();
        }

        if (statusCode == (HttpStatusCode)429)
        {
            return IsQuotaExhausted(error)
                ? new AiProviderQuotaExceededException()
                : new AiProviderRateLimitedException();
        }

        if (statusCode is HttpStatusCode.RequestTimeout)
        {
            return new AiProviderTimeoutException();
        }

        return (int)statusCode >= (int)HttpStatusCode.InternalServerError
            ? new AiProviderUnavailableException()
            : new AiProviderRequestException();
    }

    private static TimeSpan? ParseGoogleDuration(string? value)
    {
        if (value is null || !value.EndsWith('s') ||
            !double.TryParse(
                value[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds) ||
            !double.IsFinite(seconds) ||
            seconds < 0 ||
            seconds > TimeSpan.MaxValue.TotalSeconds)
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string? GetStringProperty(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? GetIntProperty(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt32(out var value)
            ? value
            : null;

    private sealed record GeminiError(
        int? Code = null,
        string? Status = null,
        string? Message = null,
        bool HasQuotaFailure = false,
        TimeSpan? RetryDelay = null,
        bool HasRateLimitReason = false);
}