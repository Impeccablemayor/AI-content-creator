using Api.Domain.Enums;

namespace Api.Application.SocialAccounts;

/// <summary>
/// The parts of a connected social account that deciding whether it may be used
/// depends on.
/// </summary>
public interface ISocialAccountCandidate
{
    Guid WorkspaceId { get; }

    ContentPlatform Platform { get; }

    SocialAccountStatus Status { get; }

    DateTime? TokenExpiresAt { get; }
}

/// <summary>Why an account was or was not selected for a platform call.</summary>
public enum ConnectedAccountSelection
{
    /// <summary>Exactly one connected account was found and it is usable.</summary>
    Selected = 1,

    /// <summary>The workspace has no account for this platform at all.</summary>
    NoAccount = 2,

    /// <summary>Accounts exist, but none of them is connected.</summary>
    NotConnected = 3,

    /// <summary>
    /// Every account for the platform is known to have expired credentials. The
    /// worker has no refresh flow, so this is reported distinctly from "not
    /// connected" because reconnection, not a retry, is what fixes it.
    /// </summary>
    Expired = 4,

    /// <summary>More than one connected account. Acting on a guess is not allowed.</summary>
    Ambiguous = 5
}

public readonly record struct ConnectedAccountSelectionResult<TAccount>(
    ConnectedAccountSelection Outcome,
    TAccount? Account) where TAccount : class
{
    public bool HasAccount => Outcome == ConnectedAccountSelection.Selected;

    public static ConnectedAccountSelectionResult<TAccount> None(ConnectedAccountSelection outcome) =>
        new(outcome, null);

    public static ConnectedAccountSelectionResult<TAccount> Chosen(TAccount account) =>
        new(ConnectedAccountSelection.Selected, account);
}

/// <summary>
/// Chooses which connected social account a platform call may be made through.
/// </summary>
/// <remarks>
/// Shared by publishing and metrics sync so a workspace with several connected
/// accounts is refused in both places rather than being published through in one
/// and measured through another.
/// </remarks>
public static class ConnectedSocialAccountSelector
{
    public static ConnectedAccountSelectionResult<TAccount> Select<TAccount>(
        IReadOnlyList<TAccount> accounts,
        DateTime now) where TAccount : class, ISocialAccountCandidate
    {
        if (accounts.Count == 0)
        {
            return ConnectedAccountSelectionResult<TAccount>.None(
                ConnectedAccountSelection.NoAccount);
        }

        var connected = accounts
            .Where(account => account.Status == SocialAccountStatus.Connected)
            .ToList();

        // Only Connected accounts are usable. Publishing or measuring with
        // credentials known to be dead would only produce a confusing provider
        // error, and anything ambiguous fails instead of guessing, because
        // picking one of several accounts would act on an account nobody chose.
        return connected.Count switch
        {
            1 => ConnectedAccountSelectionResult<TAccount>.Chosen(connected[0]),
            > 1 => ConnectedAccountSelectionResult<TAccount>.None(
                ConnectedAccountSelection.Ambiguous),
            _ => ConnectedAccountSelectionResult<TAccount>.None(
                accounts.Any(account => IsKnownExpired(account, now))
                    ? ConnectedAccountSelection.Expired
                    : ConnectedAccountSelection.NotConnected)
        };
    }

    private static bool IsKnownExpired(ISocialAccountCandidate account, DateTime now) =>
        account.Status == SocialAccountStatus.Expired ||
        (account.TokenExpiresAt is { } expiresAt && expiresAt <= now);
}
