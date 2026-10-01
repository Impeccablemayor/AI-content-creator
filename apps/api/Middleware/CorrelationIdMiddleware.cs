using System.Diagnostics;

namespace Api.Middleware;

/// <summary>
/// Carries one identifier for a request through the log output and the response.
/// </summary>
/// <remarks>
/// <para>
/// An inbound correlation identifier is reused when it is present and well formed, so
/// a caller, an ingress, or an upstream service that already started a trace can hand
/// this service the id its own logs are filed under. Otherwise the current activity's
/// trace identifier is used, and a new one is generated only when there is no ambient
/// activity at all, which is the case for a request handled outside any hosting
/// diagnostic source.
/// </para>
/// <para>
/// This deliberately does not create its own <see cref="Activity"/> and does not
/// start a span. Doing so would fork the distributed trace that the runtime and any
/// exporter already maintain; the middleware reads the existing activity and adds a
/// tag, so existing tracing behaviour is preserved and only the logging correlation is
/// added.
/// </para>
/// </remarks>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        // Error responses read TraceIdentifier, so this is what makes a failure
        // report the same id the rest of the request was logged under.
        context.TraceIdentifier = correlationId;

        if (Activity.Current is { } activity)
        {
            activity.SetTag(HeaderName, correlationId);
        }

        // The response has not started at this point in the pipeline, so the header
        // can be set directly. This keeps the id on responses that end early, such
        // as a 404, which a later stage may produce without writing a body.
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            [HeaderName] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? string.Empty
        });

        await next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var candidate = values.ToString().Trim();

            if (IsAcceptable(candidate))
            {
                return candidate;
            }
        }

        // Prefer the ambient trace so the id a log line already carries under
        // TraceId matches the correlation id.
        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Rejects a header that is absent, blank, oversized, or contains characters that
    /// would let a caller inject structure into a log line or a response header.
    /// </summary>
    private static bool IsAcceptable(string candidate)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.Length > 128)
        {
            return false;
        }

        foreach (var character in candidate)
        {
            var permitted = char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_' or '.' or ':';

            if (!permitted)
            {
                return false;
            }
        }

        return true;
    }
}
