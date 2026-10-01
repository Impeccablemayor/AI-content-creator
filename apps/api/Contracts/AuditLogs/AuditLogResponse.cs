namespace Api.Contracts.AuditLogs;

/// <summary>
/// One recorded action. Audit logs are read-only, so this contract has no
/// create or update counterpart.
/// </summary>
public sealed class AuditLogResponse
{
    public Guid Id { get; init; }

    /// <summary>
    /// What happened, for example <c>ContentApproved</c>.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// The kind of record acted on, for example <c>ContentPost</c>.
    /// </summary>
    public string ResourceType { get; init; } = string.Empty;

    /// <summary>
    /// The id of the record acted on, when the action was about one record.
    /// </summary>
    public Guid? ResourceId { get; init; }

    /// <summary>
    /// The user who performed the action. Null for a system-scoped action such
    /// as a background publish, which has no authenticated user.
    /// </summary>
    public Guid? PerformedBy { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    /// <summary>
    /// Field values before the change, as a JSON object.
    /// </summary>
    public string? OldValues { get; init; }

    /// <summary>
    /// Field values after the change, as a JSON object.
    /// </summary>
    public string? NewValues { get; init; }

    public DateTime CreatedAt { get; init; }
}
