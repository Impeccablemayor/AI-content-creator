using Api.Domain.Entities;

namespace Api.Application.Authentication;

/// <summary>
/// The one definition of which of a user's workspaces is "theirs" when the
/// session has to pick exactly one.
/// </summary>
/// <remarks>
/// <para>
/// Login has to choose a workspace before it can issue a token bound to one, and
/// it cannot ask, because the caller has not authenticated yet. Leaving the
/// choice to whatever order the database returned rows in meant the same
/// credentials could open two different workspaces on two consecutive logins.
/// </para>
/// <para>
/// <b>Oldest membership first.</b> That matches what a user already expects:
/// the workspace they joined first is the one they signed in for, and for the
/// overwhelmingly common single-workspace account it is the only row, so the
/// behaviour is unchanged. A user with several workspaces switches to whichever
/// one they want immediately after logging in, so which one opened first matters
/// far less than it being reproducible.
/// </para>
/// <para>
/// <b>Identifier as the tie-break.</b> <see cref="WorkspaceMember.JoinedAt"/> is
/// a timestamp the database stores to microsecond precision, and two memberships
/// can share it — a bulk import, or two workspaces created inside one
/// transaction. Ordering on the timestamp alone would leave the result just as
/// unstable as no ordering at all, in a case that is rarer and harder to notice.
/// The tie-break is on the membership's own identifier rather than the
/// workspace's, because those are assigned independently and only the membership
/// row is what this rule orders.
/// </para>
/// <para>
/// Declared once and used by both login and the workspace listing, because two
/// copies of an ordering rule will drift, and a switcher that disagrees with
/// login about which workspace is current is a bug that only shows up for users
/// who belong to more than one.
/// </para>
/// </remarks>
public static class WorkspaceMembershipOrder
{
    /// <summary>
    /// Orders memberships oldest-joined first, lowest membership id on a tie.
    /// </summary>
    /// <remarks>
    /// Exposed over <c>IOrderedEnumerable</c> for in-memory callers and
    /// re-expressed as an <c>IOrderedQueryable</c> for the database, because the
    /// ordering has to survive translation to SQL. A test that sorted in memory
    /// while production sorted in PostgreSQL would be testing a different rule
    /// than the one that runs.
    /// </remarks>
    public static IOrderedQueryable<WorkspaceMember> Apply(
        IQueryable<WorkspaceMember> memberships) =>
        memberships
            .OrderBy(x => x.JoinedAt)
            .ThenBy(x => x.Id);

    /// <inheritdoc cref="Apply(IQueryable{WorkspaceMember})"/>
    public static IOrderedEnumerable<WorkspaceMember> Apply(
        IEnumerable<WorkspaceMember> memberships) =>
        memberships
            .OrderBy(x => x.JoinedAt)
            .ThenBy(x => x.Id);

    /// <summary>
    /// The workspace the session opens on: the first membership by this rule, or
    /// null when the user belongs to no workspace at all.
    /// </summary>
    public static WorkspaceMember? SelectSessionMembership(
        IEnumerable<WorkspaceMember> memberships) =>
        Apply(memberships).FirstOrDefault();

    /// <summary>
    /// Whether <paramref name="candidate"/> precedes <paramref name="other"/>,
    /// by the same rule this class orders with.
    /// </summary>
    /// <remarks>
    /// For callers holding a single pair rather than a set, and used by the
    /// tests to assert the rule against explicit expectations instead of against
    /// the implementation's own output.
    /// </remarks>
    public static bool Precedes(
        WorkspaceMember candidate,
        WorkspaceMember other) =>
        candidate.JoinedAt < other.JoinedAt ||
        candidate.JoinedAt == other.JoinedAt &&
        candidate.Id.CompareTo(other.Id) < 0;
}