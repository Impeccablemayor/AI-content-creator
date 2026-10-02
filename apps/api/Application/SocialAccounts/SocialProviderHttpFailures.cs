using System.Net;

namespace Api.Application.SocialAccounts;

/// <summary>
/// Which social platform call a failure came from.
/// </summary>
/// <remarks>
/// An enum rather than the description used in the exception message, so the same
/// value can be reported as a metric label. A free-text operation label would be
/// a cardinality risk and could carry caller text.
/// </remarks>
public enum SocialProviderOperation
{
    /// <summary>Exchanging an authorization code for a token.</summary>
    OAuthToken = 1,

    /// <summary>Reading the connected account's profile.</summary>
    OAuthProfile = 2,

    /// <summary>Publishing content.</summary>
    Publish = 3,

    /// <summary>Reading a published post's metrics.</summary>
    Metrics = 4
}

/// <summary>
/// How a social platform failure is classified.
/// </summary>
public enum SocialProviderFailureClass
{
    /// <summary>The platform rejected the credentials it was given.</summary>
    Authentication = 1,

    /// <summary>The platform timed out, throttled us, or failed on its side.</summary>
    Unavailable = 2,

    /// <summary>Anything else, including a response that could not be read.</summary>
    Request = 3
}

/// <summary>
/// Maps a social platform HTTP failure onto the exception the application layer
/// reasons about.
/// </summary>
/// <remarks>
/// Shared by publishing and metrics sync so both classify a provider outage,
/// a rejected token and an unreadable response the same way. Only the status
/// code, the platform and the operation are reported: a response body can echo
/// the submitted content or a token.
/// </remarks>
public static class SocialProviderHttpFailures
{
    /// <summary>
    /// Classifies a failed provider response, counts it, and returns the exception
    /// the caller should throw.
    /// </summary>
    /// <remarks>
    /// The count happens here rather than at each call site because this is the
    /// one place a provider failure is classified. Counting at the call sites
    /// would let a new provider path be added and never counted.
    /// </remarks>
    public static Exception FromStatusCode(
        HttpStatusCode statusCode,
        SocialProviderOperation operation,
        Domain.Enums.ContentPlatform platform)
    {
        Infrastructure.Metrics.ApplicationMetrics.RecordProviderFailure(
            platform,
            operation,
            Classify(statusCode));

        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new SocialProviderAuthenticationException(
                    $"The social platform rejected the stored credentials when {Describe(operation)}."),

            // Timeouts, rate limits and server errors are transient. Reporting
            // them separately from a malformed response keeps the recorded
            // failure reason honest about whether retrying could help.
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests =>
                new SocialProviderUnavailableException(
                    $"The social platform returned HTTP {(int)statusCode} when {Describe(operation)}."),

            _ when (int)statusCode >= 500 =>
                new SocialProviderUnavailableException(
                    $"The social platform returned HTTP {(int)statusCode} when {Describe(operation)}."),

            _ => new SocialProviderRequestException(
                $"The social platform returned HTTP {(int)statusCode} when {Describe(operation)}.")
        };
    }

    /// <summary>
    /// The class a status code belongs to, without building an exception.
    /// </summary>
    public static SocialProviderFailureClass Classify(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return SocialProviderFailureClass.Authentication;
        }

        if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500)
        {
            return SocialProviderFailureClass.Unavailable;
        }

        return SocialProviderFailureClass.Request;
    }

    private static string Describe(SocialProviderOperation operation) =>
        operation switch
        {
            SocialProviderOperation.OAuthToken => "the OAuth token endpoint",
            SocialProviderOperation.OAuthProfile => "the platform profile endpoint",
            SocialProviderOperation.Publish => "publishing",
            SocialProviderOperation.Metrics => "reading post metrics",
            _ => "calling the social platform"
        };
}