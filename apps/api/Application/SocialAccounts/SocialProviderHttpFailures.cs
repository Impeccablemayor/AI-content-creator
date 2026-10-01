using System.Net;

namespace Api.Application.SocialAccounts;

/// <summary>
/// Maps a social platform HTTP failure onto the exception the application layer
/// reasons about.
/// </summary>
/// <remarks>
/// Shared by publishing and metrics sync so both classify a provider outage,
/// a rejected token and an unreadable response the same way. Only the status
/// code and the operation are reported: a response body can echo the submitted
/// content or a token.
/// </remarks>
public static class SocialProviderHttpFailures
{
    public static Exception FromStatusCode(HttpStatusCode statusCode, string operation) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new SocialProviderAuthenticationException(
                    $"The social platform rejected the stored credentials when {operation}."),

            // Timeouts, rate limits and server errors are transient. Reporting
            // them separately from a malformed response keeps the recorded
            // failure reason honest about whether retrying could help.
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests =>
                new SocialProviderUnavailableException(
                    $"The social platform returned HTTP {(int)statusCode} when {operation}."),

            _ when (int)statusCode >= 500 =>
                new SocialProviderUnavailableException(
                    $"The social platform returned HTTP {(int)statusCode} when {operation}."),

            _ => new SocialProviderRequestException(
                $"The social platform returned HTTP {(int)statusCode} when {operation}.")
        };
}
