using Microsoft.AspNetCore.Http;

namespace Api.Configuration;

/// <summary>
/// How the API stores a session in the browser: cookie names and the attributes
/// that decide whether a browser will send them back.
/// </summary>
/// <remarks>
/// <para>
/// The session lives only in httpOnly cookies. No token is ever written into a
/// response body, so page script cannot read the refresh token and there is no
/// second place for a credential to be leaked from. The access token is still a
/// bearer JWT validated by the same pipeline; the cookie is only how it reaches
/// the browser.
/// </para>
/// <para>
/// <b>SameSite is a deployment decision, not a preference.</b> A web app served
/// from the same site as the API, or from the API's own origin, sends both
/// cookies under the default <see cref="SameSiteMode.Lax"/>. A web app on a
/// different site than the API is treated as cross-site by the browser, which
/// withholds a <c>Lax</c> cookie from its cross-site requests, so such a
/// deployment must set <see cref="SameSiteMode.None"/> together with
/// <see cref="Secure"/>. Browsers reject <c>SameSite=None</c> without
/// <c>Secure</c>, so that pairing is validated at startup instead of failing
/// silently in front of real users.
/// </para>
/// <para>
/// <b>Secure defaults on outside Development</b> so a deployment cannot forget
/// it. It defaults off in Development only because the local API is reached over
/// plain HTTP, where a secure cookie is never sent back and every test would
/// fail for a reason unrelated to the code under test. Either way the value is
/// configuration and can be stated explicitly.
/// </para>
/// <para>
/// <b>Why no section appears in appsettings.json.</b> Every default lives here
/// rather than in a settings file, and that is deliberate rather than an
/// omission. The <see cref="Secure"/> default cannot be expressed in a static
/// settings file at all, because it depends on the hosting environment. The API
/// applies it in <c>Program</c> only when configuration is <i>silent</i> on the
/// key: the first value of <see cref="Secure"/> found anywhere wins. Adding
/// <c>"AuthCookies": { "Secure": true }</c> to <c>appsettings.json</c> would
/// therefore not "document" the default, it would replace it everywhere,
/// including Development, where the cookie would then never be sent back and
/// local development and the test host would break in a way that looks like an
/// authentication bug. The same applies to the other values: restating them in
/// configuration creates a second copy to keep in step with this one and buys
/// nothing, since the defaults here are already the intended ones.
/// </para>
/// <para>
/// The startup validation is what makes the silence safe. An unusable
/// combination fails the host rather than the user: see the remarks on
/// <see cref="SameSite"/>.
/// </para>
/// </remarks>
public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>
    /// The names the API uses unless configuration says otherwise. Declared here
    /// because the OpenAPI document has to name the cookie before any
    /// configuration is bound.
    /// </summary>
    public const string DefaultAccessTokenCookieName = "mayorx.at";

    public const string DefaultRefreshTokenCookieName = "mayorx.rt";

    /// <summary>
    /// The name this cookie authentication is registered under in the OpenAPI
    /// document. Declared here so the definition in
    /// <c>Program</c> and the requirement that advertises it cannot drift apart.
    /// </summary>
    public const string OpenApiSecuritySchemeName = "SessionCookie";

    public string AccessTokenCookieName { get; set; } = DefaultAccessTokenCookieName;

    public string RefreshTokenCookieName { get; set; } = DefaultRefreshTokenCookieName;

    /// <summary>
    /// Sends both cookies to every path rather than only to the API's own
    /// routes, so a request from page script needs no knowledge of the route
    /// layout to be authenticated.
    /// </summary>
    public string Path { get; set; } = "/";

    /// <summary>
    /// Left empty, which makes the cookies host-only and confines them to the
    /// host that set them. Set it only when the web app is served from a sibling
    /// subdomain of the API and a host-only cookie would not reach it.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Strict, Lax, or None. See the remarks on cross-site deployments.</summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    /// <summary>
    /// Whether the cookies carry the Secure attribute. Defaults on everywhere
    /// except Development; the effective default is applied at startup.
    /// </summary>
    public bool Secure { get; set; }

    /// <summary>
    /// Whether the configured SameSite and Secure values are one a browser will
    /// actually accept.
    /// </summary>
    /// <remarks>
    /// A browser discards a <c>SameSite=None</c> cookie that is not
    /// <c>Secure</c>, and says nothing when it does. The API would look healthy in
    /// its own logs while every cross-site user stayed logged out, so the
    /// combination is refused at startup instead.
    /// <para>
    /// Named methods rather than inline lambdas in the composition root, so the
    /// rule is one testable thing rather than a predicate buried in
    /// <c>Program</c> that no test can reach.
    /// </para>
    /// </remarks>
    public bool HasAcceptableSameSiteConfiguration() =>
        SameSite != SameSiteMode.None || Secure;

    /// <summary>
    /// Whether both cookies have a usable name. A blank name produces a cookie
    /// the API cannot read back, so it would authenticate nobody and look like
    /// it worked.
    /// </summary>
    public bool HasRequiredCookieNames() =>
        !string.IsNullOrWhiteSpace(AccessTokenCookieName) &&
        !string.IsNullOrWhiteSpace(RefreshTokenCookieName);

    /// <summary>
    /// The <see cref="Secure"/> value to use when configuration has expressed no
    /// opinion.
    /// </summary>
    /// <param name="configured">
    /// The value found in configuration, or null when the key is absent.
    /// </param>
    /// <param name="isDevelopment">
    /// Whether the host is running in the Development environment.
    /// </param>
    /// <remarks>
    /// Secure outside Development and not in it, because a developer reaches the
    /// API over plain HTTP where a secure cookie is never returned. Expressed as
    /// a function rather than left inline so the rule can be asserted directly:
    /// getting the development case wrong breaks local development and every test
    /// host for a reason that has nothing to do with the code under test.
    /// </remarks>
    public static bool ResolveSecure(bool? configured, bool isDevelopment) =>
        configured ?? !isDevelopment;
}