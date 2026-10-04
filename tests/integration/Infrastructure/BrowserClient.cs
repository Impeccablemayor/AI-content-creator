using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.IntegrationTests;

/// <summary>
/// A client that behaves like one browser tab, with a cookie jar the test owns.
/// </summary>
/// <remarks>
/// <para>
/// The jar is explicit rather than delegated to the HTTP handler. Delegating it
/// works, but then nothing in the tests can see what the browser is holding, so
/// an assertion about "the session is now scoped to this workspace" would have to
/// be inferred from the response that issued it rather than read from the
/// credentials the next request will carry. Keeping it here means the value
/// under test is the value actually sent.
/// </para>
/// <para>
/// The handler's own cookie support is switched off so there is exactly one
/// mechanism. Two would mean a test could pass because the framework silently
/// replayed a cookie the API never issued for that request.
/// </para>
/// </remarks>
public sealed class BrowserClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly Dictionary<string, string> _cookies =
        new(StringComparer.Ordinal);

    public BrowserClient(MayorxApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    /// <summary>What the browser is currently holding, by cookie name.</summary>
    public IReadOnlyDictionary<string, string> Cookies => _cookies;

    public string AccessToken =>
        Required(Api.Configuration.AuthCookieOptions.DefaultAccessTokenCookieName);

    public string RefreshToken =>
        Required(Api.Configuration.AuthCookieOptions.DefaultRefreshTokenCookieName);

    public Task<HttpResponseMessage> GetAsync(string url) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<HttpResponseMessage> PostAsync(
        string url,
        object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request)
    {
        request.Headers.Remove("Cookie");

        if (_cookies.Count > 0)
        {
            request.Headers.Add(
                "Cookie",
                string.Join(
                    "; ",
                    _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        }

        var response = await _client.SendAsync(request);

        Capture(response);

        return response;
    }

    /// <summary>Forgets everything, the way a browser does on sign-out.</summary>
    public void ClearCookies() => _cookies.Clear();

    public void Dispose() => _client.Dispose();

    private void Capture(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            return;
        }

        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0].Trim();
            var separator = pair.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator];

            // A cookie the API has already expired is a deletion. Dropping it
            // here is what makes the next request carry nothing, which is the
            // whole effect the deletion had on a real browser.
            if (IsExpired(header))
            {
                _cookies.Remove(name);

                continue;
            }

            _cookies[name] = Decode(pair[(separator + 1)..]);
        }
    }

    /// <summary>
    /// Undoes the transport encoding <c>Set-Cookie</c> applies to a value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header is a text format with a restricted character set, so a value
    /// containing <c>+</c>, <c>/</c> or <c>=</c> arrives percent-encoded. That
    /// encoding is a property of the header, not of the credential: it is
    /// reversed by the time any application code sees the cookie, whether that
    /// code is the API reading <c>Request.Cookies</c> or a test comparing the
    /// value against what the database stored.
    /// </para>
    /// <para>
    /// So the jar holds the decoded value and sends that, which round-trips to
    /// the same bytes server side. Storing the encoded form instead would mean
    /// every assertion about a token had to re-decode it first, and one that
    /// forgot would silently compare a transport form to a credential.
    /// </para>
    /// </remarks>
    private static string Decode(string value) =>
        Uri.UnescapeDataString(value);

    private string Required(string name)
    {
        if (_cookies.TryGetValue(name, out var value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"This browser is not holding a {name} cookie. It has " +
            $"{(_cookies.Count == 0 ? "no cookies" : string.Join(", ", _cookies.Keys))}.");
    }

    private static bool IsExpired(string setCookieHeader)
    {
        var segment = setCookieHeader
            .Split(';')
            .FirstOrDefault(part =>
                part.TrimStart()
                    .StartsWith("expires=", StringComparison.OrdinalIgnoreCase));

        if (segment is null)
        {
            // No expiry means a session cookie, which lasts until the browser
            // closes and is therefore not a deletion.
            return false;
        }

        if (!DateTimeOffset.TryParse(
                segment[(segment.IndexOf('=') + 1)..].Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var expires))
        {
            return false;
        }

        return expires <= DateTimeOffset.UtcNow;
    }
}