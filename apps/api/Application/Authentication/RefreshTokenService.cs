using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.Authentication;

/// <summary>
/// The credentials a successful refresh produces.
/// </summary>
public sealed record RefreshedSession(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid WorkspaceId);

/// <summary>
/// Issues, rotates, and revokes refresh tokens.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Issues a token for a user and workspace. Returns the plaintext once.</summary>
    Task<string> IssueAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for a new pair, consuming the presented token.
    /// </summary>
    Task<RefreshedSession> RefreshAsync(
        string presentedToken,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a refresh token. Idempotent: an unknown or already revoked token
    /// is not an error. Persistence failures are not suppressed.
    /// </summary>
    Task RevokeAsync(
        string presentedToken,
        CancellationToken cancellationToken);
}

/// <summary>
/// Refresh token lifecycle, with rotation and reuse detection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rotation is atomic.</b> The replacement row is inserted and the
/// consumed row is marked revoked inside one transaction, and the marking is a
/// conditional UPDATE that only succeeds while the row is still
/// <see cref="RefreshTokenStatus.Active"/>. If two requests present the same
/// token, exactly one affects a row; the other affects none and is told it lost
/// the race. No read-then-write gap exists for a second request to slip into.
/// </para>
/// <para>
/// <b>Reuse is detected, not just refused.</b> Presenting a token that rotation
/// already consumed is treated as a replay: the token is marked
/// <see cref="RefreshTokenStatus.Reused"/> and the traversal walks
/// <c>ReplacedById</c> forward through every later generation, revoking the live
/// descendant. That is what stops a stolen token from outliving the theft.
/// The walk is bounded and filtered by the original user and workspace, so it
/// neither loops forever nor follows a link out of the session it came from.
/// </para>
/// <para>
/// A row revoked with no replacement is a logout, not a consumed token, and is
/// reported as revoked without touching any descendant. Likewise a request that
/// merely lost a concurrent rotation is reported as a conflict rather than
/// reuse, because one client double-submitting is indistinguishable from an
/// attack without guessing, and guessing wrong would log out legitimate users.
/// </para>
/// <para>
/// Ownership is resolved from the stored row, never from the request, so a
/// token cannot be redeemed for a workspace it was not issued for.
/// </para>
/// </remarks>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private const string RotationReason = "Rotated";
    private const string ReuseReason = "Reused";
    private const string ReuseRevocationReason = "RevokedByReuseDetection";
    private const string LogoutReason = "Logout";
    private const string ExpiryReason = "Expired";

    /// <summary>
    /// Bounds the reuse traversal. A chain longer than this is abandoned
    /// rather than walked, which keeps a malformed graph from turning a single
    /// request into an unbounded loop.
    /// </summary>
    private const int MaxGenerationDepth = 1000;

    private readonly AppDbContext _dbContext;
    private readonly JwtTokenService _jwtTokenService;
    private readonly JwtOptions _options;

    public RefreshTokenService(
        AppDbContext dbContext,
        JwtTokenService jwtTokenService,
        IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _options = options.Value;
    }

    /// <summary>
    /// Base64 SHA-256. Only this is persisted, so the stored value cannot be
    /// turned back into a usable token.
    /// </summary>
    public static string HashToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return Convert.ToBase64String(digest);
    }

    public async Task<string> IssueAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var plaintext = RefreshTokenGenerator.Generate();
        var now = DateTime.UtcNow;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            TokenHash = HashToken(plaintext),
            ExpiresAt = now.AddDays(_options.RefreshTokenExpirationDays),
            Status = RefreshTokenStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return plaintext;
    }

    public async Task<RefreshedSession> RefreshAsync(
        string presentedToken,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // A blank or absent token is a malformed request, not a server fault.
        // Checked before hashing so it is answered as an invalid token rather
        // than surfacing as an unmapped 500.
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            throw new InvalidRefreshTokenException();
        }

        var current = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                token => token.TokenHash == HashToken(presentedToken),
                cancellationToken);

        if (current is null)
        {
            throw new InvalidRefreshTokenException();
        }

        // Expiry is checked first so an expired token reads as expired even if
        // it was also revoked.
        if (current.Status == RefreshTokenStatus.Expired ||
            current.ExpiresAt <= now)
        {
            await MarkExpiredAsync(current.Id, now, cancellationToken);

            throw new ExpiredRefreshTokenException();
        }

        // A replacement link means rotation consumed this token. Presenting it
        // again is a replay, so the descendant session is revoked.
        if (current.ReplacedById is not null)
        {
            await HandleReuseAsync(current, now, cancellationToken);

            throw new ReusedRefreshTokenException();
        }

        // Revoked with no replacement: logout.
        if (current.Status != RefreshTokenStatus.Active)
        {
            throw new RevokedRefreshTokenException();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == current.UserId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new RefreshTokenOwnerInactiveException();
        }

        var stillAMember = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == current.UserId &&
                    membership.WorkspaceId == current.WorkspaceId,
                cancellationToken);

        if (!stillAMember)
        {
            throw new RefreshTokenWorkspaceAccessRevokedException();
        }

        var replacementId = Guid.NewGuid();
        var replacementPlaintext = RefreshTokenGenerator.Generate();

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = replacementId,
            UserId = current.UserId,
            WorkspaceId = current.WorkspaceId,
            TokenHash = HashToken(replacementPlaintext),
            ExpiresAt = now.AddDays(_options.RefreshTokenExpirationDays),
            Status = RefreshTokenStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        // The compare-and-swap. The predicate is re-evaluated by PostgreSQL after
        // any competing writer releases the row lock, so the loser of a race
        // sees zero rows affected rather than overwriting the winner.
        var consumed = await _dbContext.RefreshTokens
            .Where(token =>
                token.Id == current.Id &&
                token.Status == RefreshTokenStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.Status, RefreshTokenStatus.Revoked)
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, RotationReason)
                    .SetProperty(token => token.ReplacedById, replacementId)
                    .SetProperty(token => token.UpdatedAt, now),
                cancellationToken);

        if (consumed != 1)
        {
            await transaction.RollbackAsync(cancellationToken);

            throw new RefreshTokenRotationConflictException();
        }

        await transaction.CommitAsync(cancellationToken);

        var (accessToken, expiresAt) =
            _jwtTokenService.GenerateToken(user, current.WorkspaceId);

        return new RefreshedSession(
            accessToken,
            expiresAt,
            replacementPlaintext,
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            current.WorkspaceId);
    }

    public async Task RevokeAsync(
        string presentedToken,
        CancellationToken cancellationToken)
    {
        // An absent token is a client that already discarded it. Nothing to
        // revoke, and not an error.
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return;
        }

        // No catch. If the revocation cannot be persisted the request must fail
        // rather than report a logout that did not happen.
        await _dbContext.RefreshTokens
            .Where(token =>
                token.TokenHash == HashToken(presentedToken) &&
                token.Status == RefreshTokenStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.Status, RefreshTokenStatus.Revoked)
                    .SetProperty(token => token.RevokedAt, DateTime.UtcNow)
                    .SetProperty(
                        token => token.RevocationReason,
                        LogoutReason)
                    .SetProperty(token => token.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    private async Task MarkExpiredAsync(
        Guid tokenId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _dbContext.RefreshTokens
            .Where(token =>
                token.Id == tokenId &&
                token.Status == RefreshTokenStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.Status, RefreshTokenStatus.Expired)
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, ExpiryReason)
                    .SetProperty(token => token.UpdatedAt, now),
                cancellationToken);
    }

    private async Task HandleReuseAsync(
        RefreshToken consumed,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.RefreshTokens
            .Where(token =>
                token.Id == consumed.Id &&
                token.Status == RefreshTokenStatus.Revoked)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.Status, RefreshTokenStatus.Reused)
                    .SetProperty(token => token.RevocationReason, ReuseReason)
                    .SetProperty(token => token.UpdatedAt, now),
                cancellationToken);

        await RevokeDescendantsAsync(
            consumed.ReplacedById!.Value,
            consumed.UserId,
            consumed.WorkspaceId,
            now,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Walks the replacement chain forward from a consumed token and revokes
    /// every generation still active, so the session a stolen token spawned
    /// cannot outlive the theft.
    /// </summary>
    private async Task RevokeDescendantsAsync(
        Guid startId,
        Guid userId,
        Guid workspaceId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var frontier = new List<Guid> { startId };

        for (var generation = 0;
             generation < MaxGenerationDepth && frontier.Count > 0;
             generation++)
        {
            // Every query is filtered by the original user and workspace. That is
            // the guard that keeps the walk inside the session it started from,
            // whatever a generation link claims.
            var links = await _dbContext.RefreshTokens
                .AsNoTracking()
                .Where(token =>
                    token.UserId == userId &&
                    token.WorkspaceId == workspaceId &&
                    frontier.Contains(token.Id))
                .Select(token => new
                {
                    token.Id,
                    token.ReplacedById,
                    token.Status
                })
                .ToListAsync(cancellationToken);

            if (links.Count == 0)
            {
                return;
            }

            var activeIds = links
                .Where(link => link.Status == RefreshTokenStatus.Active)
                .Select(link => link.Id)
                .ToList();

            if (activeIds.Count > 0)
            {
                await _dbContext.RefreshTokens
                    .Where(token =>
                        activeIds.Contains(token.Id) &&
                        token.Status == RefreshTokenStatus.Active)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                token => token.Status,
                                RefreshTokenStatus.Revoked)
                            .SetProperty(token => token.RevokedAt, now)
                            .SetProperty(
                                token => token.RevocationReason,
                                ReuseRevocationReason)
                            .SetProperty(token => token.UpdatedAt, now),
                        cancellationToken);
            }

            frontier = links
                .Where(link => link.ReplacedById is not null)
                .Select(link => link.ReplacedById!.Value)
                .ToList();
        }
    }
}
