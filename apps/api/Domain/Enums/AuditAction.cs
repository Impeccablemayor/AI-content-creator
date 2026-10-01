namespace Api.Domain.Enums;

/// <summary>
/// A significant, user-visible action recorded in the audit log.
/// </summary>
/// <remarks>
/// Values are the names the API specification shows in its audit log example
/// (`ContentApproved`). The database schema sketches the same set in
/// SCREAMING_SNAKE_CASE; PascalCase is used because every other enum in the
/// model is stored as a string in its .NET form (see
/// <c>HasConversion&lt;string&gt;()</c> in <c>AppDbContext</c>), and because
/// the API example is the client-facing contract.
/// <para>
/// The set is intentionally small. An enum here means "this action is
/// deliberately traceable", not "every method call is logged". Actions with no
/// writer yet are documented as deferred in the API specification rather than
/// declared and left unpopulated.
/// </para>
/// </remarks>
public enum AuditAction
{
    /// <summary>An idea or post was approved.</summary>
    ContentApproved = 1,

    /// <summary>An idea or post was rejected.</summary>
    ContentRejected = 2,

    /// <summary>Content was deleted.</summary>
    ContentDeleted = 3,

    /// <summary>A social account finished connecting.</summary>
    AccountConnected = 4,

    /// <summary>A social account was disconnected.</summary>
    AccountDisconnected = 5,

    /// <summary>A post was scheduled for publication.</summary>
    ScheduleCreated = 6,

    /// <summary>A scheduled post's schedule was cancelled.</summary>
    ScheduleCancelled = 7,

    /// <summary>A post was published to a platform.</summary>
    Published = 8
}
