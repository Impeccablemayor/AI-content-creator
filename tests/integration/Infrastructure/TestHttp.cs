using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Auth;
using Api.Contracts.Responses;

namespace Api.IntegrationTests;

/// <summary>
/// A registered user and the token that identifies them.
/// </summary>
public sealed record TestSession(Guid WorkspaceId, string AccessToken)
{
    public static async Task<TestSession> RegisterAsync(
        HttpClient client,
        string suffix)
    {
        var response = await client.PostAsJsonAsync(
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

        return new TestSession(body.WorkspaceId, body.AccessToken);
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
}