using Api.Configuration;
using Api.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Middleware;

/// <summary>
/// Caps how large a request body the API will read.
/// </summary>
/// <remarks>
/// <para>
/// The limit is checked before the body is buffered and is expressed as 413
/// with the same error envelope every other failure uses, so a client sees one
/// response shape rather than an empty 413 from the server.
/// </para>
/// <para>
/// The bound is generous on purpose. Content generation posts carry long
/// bodies, and the OAuth callback is a GET with its inputs on the query string,
/// so neither is close to the ceiling; a limit low enough to be interesting as
/// abuse protection would be low enough to break real work.
/// </para>
/// <para>
/// A request that declares no length is not rejected here. Chunked bodies are
/// bounded by Kestrel's own limit instead, which fails at the socket rather than
/// after the framework has already committed to reading.
/// </para>
/// </remarks>
public sealed class RequestSizeLimitMiddleware
{
    private const string SectionName = "RequestSizeLimit";

    private const string MaxSizeSettingName = $"{SectionName}:MaxRequestBodySizeInBytes";

    /// <summary>
    /// Large enough for long content generation posts, small enough that an
    /// oversized body is refused before it is read.
    /// </summary>
    public const long DefaultMaxRequestBodySizeInBytes = 8 * 1024 * 1024;

    private readonly RequestDelegate _next;
    private readonly long _maxRequestBodySizeInBytes;
    private readonly JsonSerializerOptions _jsonOptions;

    public RequestSizeLimitMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IOptions<JsonOptions> mvcJsonOptions)
    {
        _next = next;
        _jsonOptions = mvcJsonOptions.Value.JsonSerializerOptions;

        // Configurable so a deployment, or a test, can move the ceiling without a
        // rebuild. A value that is present but not a positive number falls back
        // to the default rather than silently disabling the limit.
        _maxRequestBodySizeInBytes =
            configuration.GetValue<long?>(MaxSizeSettingName)
            is > 0 and { } configured
                ? configured
                : DefaultMaxRequestBodySizeInBytes;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var contentLength = context.Request.ContentLength;

        if (contentLength is > 0 &&
            contentLength.Value > _maxRequestBodySizeInBytes)
        {
            context.Response.StatusCode =
                StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/json";

            var response = new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "REQUEST_TOO_LARGE",
                    Message =
                        "The request body exceeds the maximum allowed size " +
                        $"of {_maxRequestBodySizeInBytes} bytes."
                },
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, _jsonOptions),
                context.RequestAborted);

            return;
        }

        await _next(context);
    }
}
