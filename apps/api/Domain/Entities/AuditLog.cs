using System.Net;
using Api.Domain.Enums;

namespace Api.Domain.Entities;

/// <summary>
/// An append-only record of a significant action taken in a workspace.
/// </summary>
/// <remarks>
/// This is the entity the design calls `audit_logs`. It exists to answer
/// "who did what, to which record, and when" after the fact, so it is written
/// as a side effect of the action rather than queried to drive behaviour.
/// <para>
/// Rows are immutable. There is deliberately no update path and no delete
/// endpoint, and the workspace relationship is Restrict so deleting a workspace
/// cannot quietly erase its history.
/// </para>
/// </remarks>
public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// The user who performed the action, when there was one.
    /// </summary>
    /// <remarks>
    /// Nullable because the documented <see cref="AuditAction.Published"/>
    /// action is performed by the background worker, which is system-scoped and
    /// has no authenticated user. Every action written today does have one.
    /// </remarks>
    public Guid? UserId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>
    /// What kind of record was acted on, for example <c>ContentPost</c>.
    /// </summary>
    /// <remarks>
    /// The API specification's example shows <c>ContentDraft</c>, but there is
    /// no draft table: drafts and platform variants both live on
    /// <c>content_posts</c>. This records the table name that actually exists.
    /// </remarks>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The id of the record that was acted on. A system-wide action that is not
    /// about one record may leave this null.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Field values before the change, as a JSON object. Null when not recorded.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Field values after the change, as a JSON object. Null when not recorded.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// The remote address the request came from. Null when unavailable, which
    /// includes background actions.
    /// </summary>
    /// <remarks>
    /// Typed as <see cref="IPAddress"/> because the column is PostgreSQL INET
    /// and the provider maps that type natively. A string would need a value
    /// converter, and a plain string would also let an unvalidated value reach
    /// the column.
    /// </remarks>
    public IPAddress? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
