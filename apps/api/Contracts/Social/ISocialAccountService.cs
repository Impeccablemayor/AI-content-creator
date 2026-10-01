using Api.Contracts.Social;

namespace Api.Contracts.Social;

public interface ISocialAccountService
{
    Task<IReadOnlyList<SocialAccountResponse>> GetAsync(
        CancellationToken cancellationToken);

    Task<SocialConnectResponse> CreateAuthorizationAsync(
        string platform,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes an OAuth callback. The workspace is taken from the stored
    /// single-use state rather than the caller, because the provider redirects
    /// the browser without an Authorization header.
    /// </summary>
    Task<SocialAccountResponse?> CompleteAsync(
        string platform,
        string code,
        string state,
        CancellationToken cancellationToken);

    Task<SocialAccountResponse?> DisconnectAsync(
        Guid id,
        CancellationToken cancellationToken);
}
