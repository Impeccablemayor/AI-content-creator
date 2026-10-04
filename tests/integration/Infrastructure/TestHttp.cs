using System.Text.Json;
using Api.Contracts.Auth;
using Api.Contracts.Responses;

namespace Api.IntegrationTests;

/// <summary>
/// A registered user and the browser session that was issued for them.
/// </summary>
/// <remarks>
/// The tokens come out of the browser's cookie jar rather than the response body,
/// because that is now the only place the API puts them. A session that read them
/// from the body would be testing a contract the API no longer has.
/// </remarks>
public sealed record TestSession(
    Guid WorkspaceId,
    string AccessToken,
    Guid UserId = default,
    string RefreshToken = "",
    string Email = "")
{
    public static async Task<TestSession> RegisterAsync(
        BrowserClient browser,
        string suffix)
    {
        var response = await browser.PostAsync(
            "api/v1/auth/register",
            new
            {
                email = $"owner-{suffix}@example.test",
                password = "Integration-Test-Password-42",
                firstName = "Integration",
                lastName = "Owner",
                workspaceName = $"Workspace {suffix}"
            });

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        return new TestSession(
            body.WorkspaceId,
            browser.AccessToken,
            body.User.Id,
            browser.RefreshToken,
            body.User.Email);
    }
}

/// <summary>
/// Reads the API's own response envelope, so a test fails with the body the API
/// produced rather than on a null reference somewhere further down.
/// </summary>
public static class TestHttp
{
    public static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException(
                $"Expected a JSON body from {response.RequestMessage?.RequestUri}, " +
                $"but the response was empty ({(int)response.StatusCode}).");
        }

        var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, Json)
            ?? throw new InvalidOperationException(
                $"Could not read an API response from '{body}'.");

        return envelope.Data;
    }

    /// <summary>
    /// Adds or replaces the cookie on an outgoing request.
    /// </summary>
    /// <remarks>
    /// Replaces rather than appends. A browser sends one cookie per name, and a
    /// test that stacks a second value for a name would be describing something
    /// no browser can produce. Every cookie this API sets has Path=/, so one
    /// header carrying one pair is an exact description of what a browser would
    /// send to any route.
    /// </remarks>
    public static HttpRequestMessage WithCookie(
        this HttpRequestMessage request,
        string name,
        string value)
    {
        request.Headers.Remove("Cookie");
        request.Headers.Add("Cookie", $"{name}={value}");

        return request;
    }

    public static void EnsureSucceeded(
        HttpResponseMessage response,
        object? body = null)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} " +
            $"returned {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}");
    }

    /// <summary>
    /// The whole <c>Set-Cookie</c> header the API returned for one cookie name.
    /// </summary>
    public static string SetCookieFor(
        HttpResponseMessage response,
        string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            throw new InvalidOperationException(
                $"Expected a {name} cookie on " +
                $"{response.RequestMessage?.Method} " +
                $"{response.RequestMessage?.RequestUri}, but no Set-Cookie " +
                "header was returned.");
        }

        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0].Trim();
            var separator = pair.IndexOf('=');

            if (separator > 0 &&
                pair.AsSpan(0, separator).SequenceEqual(name))
            {
                return header;
            }
        }

        throw new InvalidOperationException(
            $"Expected a {name} cookie on " +
            $"{response.RequestMessage?.Method} " +
            $"{response.RequestMessage?.RequestUri}, but the headers " +
            $"returned were: {string.Join(" | ", headers)}");
    }

    /// <summary>
    /// The credential the API put in a cookie, ready to be presented again.
    /// </summary>
    /// <remarks>
    /// The decoded value rather than the header. A test that needs to know what
    /// the API promised about the cookie reads the header; a test that needs to
    /// authenticate with it reads this. Handing a caller the whole header would
    /// let <c>expires</c> and <c>httponly</c> be sent as part of a bearer token,
    /// which fails for reasons that have nothing to do with what is being tested.
    /// </remarks>
    public static string ReadSetCookie(
        HttpResponseMessage response,
        string name) =>
        Uri.UnescapeDataString(
            SetCookieFor(response, name).Split(';', 2)[0]
                .Split('=', 2)[1]
                .Trim());

    /// <summary>
    /// When a <c>Set-Cookie</c> header says the cookie stops being valid.
    /// </summary>
    /// <remarks>
    /// Whole headers are taken one at a time. A comma appears inside an
    /// <c>Expires</c> date, so folding several cookies into one string and
    /// splitting on commas would cut a value in half.
    /// </remarks>
    public static DateTimeOffset CookieExpiryOf(string setCookieHeader)
    {
        var segment = setCookieHeader
            .Split(';')
            .FirstOrDefault(part =>
                part.TrimStart()
                    .StartsWith("expires=", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"The cookie '{setCookieHeader}' carries no Expires attribute.");

        return DateTimeOffset.Parse(
            segment[(segment.IndexOf('=') + 1)..].Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}