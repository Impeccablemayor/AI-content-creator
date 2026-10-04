using System.Text;
using System.Text.Json;

namespace Api.IntegrationTests;

/// <summary>
/// Reads claims out of an access token the way a relying party would, without
/// taking a dependency on a JWT library.
/// </summary>
/// <remarks>
/// Decoding the payload is enough and nothing here trusts it. The token is
/// already cryptographically verified by the API before it authorises anything,
/// so a test that reads a claim is inspecting a credential the server accepted,
/// not vouching for one it produced.
/// </remarks>
public static class JwtClaims
{
    public static Guid ReadWorkspaceId(string accessToken) =>
        Guid.Parse(ReadWorkspaceClaim(accessToken));

    public static string ReadWorkspaceClaim(string accessToken)
    {
        var segments = accessToken.Split('.');

        if (segments.Length != 3)
        {
            throw new InvalidOperationException(
                $"Expected a three-segment JWT, but got " +
                $"{segments.Length} segments.");
        }

        var payload = Encoding.UTF8.GetString(
            Base64UrlDecode(segments[1]));

        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty(
                "workspace_id",
                out var workspaceId))
        {
            throw new InvalidOperationException(
                $"The token carries no workspace_id claim. Claims present: " +
                string.Join(", ", document.RootElement
                    .EnumerateObject()
                    .Select(property => property.Name)) + ".");
        }

        return workspaceId.GetString()
            ?? throw new InvalidOperationException(
                "The workspace_id claim was not a string.");
    }

    private static byte[] Base64UrlDecode(string value)
    {
        // base64url uses two characters base64 has no use for, and omits the
        // padding that the decoder insists on.
        var standard = value
            .Replace('-', '+')
            .Replace('_', '/');

        return Convert.FromBase64String(
            standard.PadRight(
                standard.Length + ((4 - (standard.Length % 4)) % 4),
                '='));
    }
}