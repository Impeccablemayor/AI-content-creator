using System.Text.Json;
using System.Net;
using Api.Application.Authentication;
using Api.Contracts.AuditLogs;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Api.Application.AuditLogs;

public interface IAuditLogService
{
    Task<IReadOnlyList<AuditLogResponse>> GetAsync(
        AuditAction? action,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an action against the caller's workspace.
    /// </summary>
    /// <remarks>
    /// The current user and the request's IP address and user agent are captured
    /// from the ambient HTTP context, so callers only state what happened.
    /// </remarks>
    Task RecordAsync(
        AuditAction action,
        string entityType,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an action against an explicitly named workspace and user.
    /// </summary>
    /// <remarks>
    /// Needed where the acting workspace is not the caller's own workspace and
    /// cannot be read from the ambient context, such as the social OAuth
    /// callback, which is anonymous and resolves its workspace from the
    /// signed state instead of a token.
    /// </remarks>
    Task RecordAsync(
        Guid workspaceId,
        Guid? userId,
        AuditAction action,
        string entityType,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}

public sealed class AuditLogService : IAuditLogService
{
    /// <summary>
    /// Longest user agent stored. A client can send an arbitrarily long header,
    /// and the column is TEXT, but an unbounded value in an audit row is not
    /// useful and would let a caller bloat the table.
    /// </summary>
    private const int MaxUserAgentLength = 512;

    /// <summary>
    /// Upper bound on a single page. The endpoint is not paginated yet, so
    /// without this an unbounded read would be possible.
    /// </summary>
    private const int MaxRows = 200;

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        // Keeps the stored JSON close to what a human reading the log expects,
        // and avoids escaping characters that are common in prose.
        WriteIndented = false
    };

    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<AuditLogResponse>> GetAsync(
        AuditAction? action,
        string? entityType,
        Guid? entityId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var query = _dbContext.AuditLogs.AsNoTracking()
            .Where(log => log.WorkspaceId == workspaceId);

        if (action.HasValue)
        {
            query = query.Where(log => log.Action == action.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalized = entityType.Trim();
            query = query.Where(log => log.EntityType == normalized);
        }

        if (entityId.HasValue)
        {
            query = query.Where(log => log.EntityId == entityId.Value);
        }

        // Newest first, because the question an audit log answers is "what just
        // happened". Id breaks ties so paging and repeated calls are stable.
        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return logs.Select(Map).ToList();
    }

    public Task RecordAsync(
        AuditAction action,
        string entityType,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            _currentUser.WorkspaceId,
            // Null rather than throwing when unauthenticated: a background
            // action has no user but is still worth recording.
            _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            action,
            entityType,
            entityId,
            oldValues,
            newValues,
            cancellationToken);

    public Task RecordAsync(
        Guid workspaceId,
        Guid? userId,
        AuditAction action,
        string entityType,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        var httpContext = _httpContextAccessor.HttpContext;

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Action = action,
            EntityType = entityType.Trim(),
            EntityId = entityId,
            OldValues = Serialize(oldValues),
            NewValues = Serialize(newValues),
            IpAddress = ResolveIpAddress(httpContext),
            UserAgent = TruncateUserAgent(httpContext?.Request.Headers.UserAgent),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(log);

        // Deliberately not saved here. The caller's unit of work is saved once,
        // so the audit row lands in the same transaction as the change it
        // describes. A failure therefore leaves neither applied.
        return Task.CompletedTask;
    }

    private static string? Serialize(object? values) =>
        values is null ? null : JsonSerializer.Serialize(values, PayloadOptions);

    private static IPAddress? ResolveIpAddress(HttpContext? httpContext)
    {
        var value = httpContext?.Connection.RemoteIpAddress;
        if (value is null)
        {
            return null;
        }

        // Map IPv4-mapped IPv6 (::ffff:203.0.113.5) to the plain address so
        // the INET column stores the address itself rather than the transport's
        // representation of it. The scope id is dropped for the same reason.
        return value.IsIPv4MappedToIPv6 ? value.MapToIPv4() : value;
    }

    private static string? TruncateUserAgent(StringValues? userAgent)
    {
        if (userAgent is null || StringValues.IsNullOrEmpty(userAgent.Value))
        {
            return null;
        }

        var value = userAgent.Value.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= MaxUserAgentLength
            ? value
            : value[..MaxUserAgentLength];
    }

    private static AuditLogResponse Map(AuditLog log) => new()
    {
        Id = log.Id,
        Action = log.Action.ToString(),
        ResourceType = log.EntityType,
        ResourceId = log.EntityId,
        PerformedBy = log.UserId,
        IpAddress = log.IpAddress?.ToString(),
        UserAgent = log.UserAgent,
        OldValues = log.OldValues,
        NewValues = log.NewValues,
        CreatedAt = log.CreatedAt
    };
}
