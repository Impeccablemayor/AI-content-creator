using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Reads the health envelope. It is the API's usual <c>data</c> wrapper rather than
/// the flat document the framework's own writer produces.
/// </summary>
public static class HealthBody
{
    /// <summary>
    /// Returns the <c>data</c> element of the envelope, which is where the health
    /// fields live.
    /// </summary>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement
                .GetProperty("data")
                .Clone();
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException)
        {
            throw new InvalidOperationException(
                $"Expected an enveloped health body from " +
                $"{response.RequestMessage?.RequestUri} " +
                $"({(int)response.StatusCode}), but the body was '{body}'.", exception);
        }
    }

    /// <summary>
    /// Asserts the status code, quoting the body when it does not match. A health
    /// failure is nearly always explained by the body, so omitting it turns a one
    /// line diagnosis into a log dive.
    /// </summary>
    public static async Task AssertStatusAsync(
        HttpResponseMessage response,
        HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();

        Assert.Fail(
            $"{response.RequestMessage?.RequestUri} returned " +
            $"{(int)response.StatusCode} ({response.ReasonPhrase}), " +
            $"expected {(int)expected}. Body: {body}");
    }
}