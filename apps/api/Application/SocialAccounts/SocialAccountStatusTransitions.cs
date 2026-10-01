using Api.Domain.Enums;

namespace Api.Application.SocialAccounts;

public static class SocialAccountStatusTransitions
{
    /// <summary>
    /// A connection can be dropped while it is still live or recoverable.
    /// A revoked or already disconnected account has nothing left to revoke.
    /// </summary>
    public static bool CanDisconnect(SocialAccountStatus status) =>
        status is SocialAccountStatus.Connected
            or SocialAccountStatus.Expired
            or SocialAccountStatus.Error;

    /// <summary>
    /// Re-authorising is allowed from any state, including a previously
    /// disconnected or revoked account.
    /// </summary>
    public static bool CanConnect(SocialAccountStatus status) =>
        Enum.IsDefined(status);

    public static bool HasUsableCredentials(SocialAccountStatus status) =>
        status is SocialAccountStatus.Connected or SocialAccountStatus.Expired;
}
