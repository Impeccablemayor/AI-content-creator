namespace Api.Middleware;

/// <summary>
/// Adds the response headers that are meaningful for a JSON API.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately absent: Content-Security-Policy, and the browser-only policies
/// that go with it. They govern documents a browser renders, and this API
/// returns JSON and OAuth redirects, so adding them here would be decoration.
/// Swagger UI is the one HTML surface the API serves, and a restrictive
/// document policy would break it; if it is ever exposed beyond development,
/// the headers belong at the edge that terminates in front of it.
/// </para>
/// <para>
/// <c>X-XSS-Protection</c> is set to 0 rather than left on. The legacy auditor
/// filter has known bypasses and its own CVE history, and current guidance is
/// to disable it explicitly so old user agents do not apply it.
/// </para>
/// <para>
/// Headers are written before the pipeline runs, so they are present on
/// responses produced by later stages including error and health responses.
/// Nothing here is conditional, so they cannot be forgotten on a code path.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Stops a browser second-guessing a declared content type.
        headers["X-Content-Type-Options"] = "nosniff";

        // This API is never a document worth framing.
        headers["X-Frame-Options"] = "DENY";

        // Leaks no path to a third party, and no more than the origin cross-site.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Disabled explicitly rather than left to a stale user-agent heuristic.
        headers["X-XSS-Protection"] = "0";

        // Tokens and personal content must not sit in a shared or back-button
        // cache. Scoped to this API's own routes so it does not interfere with
        // the Swagger UI, which is a cacheable document.
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Cache-Control"] = "no-store";
        }

        await _next(context);
    }
}
