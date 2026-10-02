using System.Security.Cryptography;

namespace Api.Application.Authentication;

public static class RefreshTokenGenerator
{
    public static string Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
