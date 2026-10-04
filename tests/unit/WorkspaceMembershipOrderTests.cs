using Api.Application.Authentication;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Which workspace a session opens on, and in what order a user's workspaces are
/// listed.
/// </summary>
/// <remarks>
/// <para>
/// Login cannot ask which workspace the caller wants, because the caller has not
/// authenticated yet. It has to choose, and an unchosen choice means the same
/// credentials can open different workspaces on different days depending on the
/// order the database happened to return rows in.
/// </para>
/// <para>
/// These tests assert the rule against explicit expectations built by hand, not
/// against the implementation's own output. A test that sorted with the
/// implementation and compared it to itself would pass whatever the rule turned
/// out to be.
/// </para>
/// </remarks>
public sealed class WorkspaceMembershipOrderTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTime Early =
        new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Late =
        new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------ selecting a session

    [Fact]
    public void A_user_with_one_workspace_opens_it()
    {
        var only = Membership(joinedAt: Late);

        Assert.Equal(
            only.WorkspaceId,
            WorkspaceMembershipOrder
                .SelectSessionMembership([only])!
                .WorkspaceId);
    }

    [Fact]
    public void A_user_with_no_workspace_selects_nothing()
    {
        // Reached when an account exists with no membership at all. Returning null
        // is what lets login answer honestly rather than mint a session bound to
        // nothing.
        Assert.Null(
            WorkspaceMembershipOrder.SelectSessionMembership(
                Array.Empty<WorkspaceMember>()));
    }

    [Fact]
    public void The_oldest_membership_wins_regardless_of_input_order()
    {
        var newest = Membership(joinedAt: Late);
        var oldest = Membership(joinedAt: Early);
        var middle = Membership(joinedAt: new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        // Every permutation, because the defect being guarded against was
        // precisely that input order leaked into the answer. One arrangement
        // would pass even if the ordering were not applied at all.
        foreach (var arrangement in new WorkspaceMember[][]
                 {
                     [newest, middle, oldest],
                     [oldest, newest, middle],
                     [middle, oldest, newest],
                     [newest, oldest, middle]
                 })
        {
            Assert.Equal(
                oldest.WorkspaceId,
                WorkspaceMembershipOrder
                    .SelectSessionMembership(arrangement)!
                    .WorkspaceId);
        }
    }

    [Fact]
    public void Two_memberships_sharing_a_join_time_are_broken_by_identifier()
    {
        // A bulk import, or two workspaces created inside one transaction, writes
        // memberships with an identical timestamp. Ordering on the timestamp alone
        // leaves the result just as unstable as no ordering, in a rarer and
        // harder-to-notice case.
        var lowerId = Membership(
            joinedAt: Early,
            id: Guid.Parse("00000000-0000-0000-0000-00000000000a"));

        var higherId = Membership(
            joinedAt: Early,
            id: Guid.Parse("00000000-0000-0000-0000-00000000000b"));

        Assert.Equal(
            lowerId.WorkspaceId,
            WorkspaceMembershipOrder
                .SelectSessionMembership([higherId, lowerId])!
                .WorkspaceId);

        Assert.Equal(
            lowerId.WorkspaceId,
            WorkspaceMembershipOrder
                .SelectSessionMembership([lowerId, higherId])!
                .WorkspaceId);
    }

    [Fact]
    public void The_join_time_beats_the_identifier()
    {
        // Otherwise the tie-break would quietly become the primary key and the
        // oldest workspace would stop being the one login opens.
        var newestWithLowestId = Membership(
            joinedAt: Late,
            id: Guid.Parse("00000000-0000-0000-0000-00000000000a"));

        var oldestWithHighestId = Membership(
            joinedAt: Early,
            id: Guid.Parse("00000000-0000-0000-0000-00000000000f"));

        Assert.Equal(
            oldestWithHighestId.WorkspaceId,
            WorkspaceMembershipOrder
                .SelectSessionMembership(
                    [newestWithLowestId, oldestWithHighestId])!
                .WorkspaceId);
    }

    [Fact]
    public void A_membership_joined_a_microsecond_earlier_still_wins()
    {
        // The tie-break exists because PostgreSQL stores timestamps to microsecond
        // precision, so two memberships really can share one to the last digit the
        // column keeps. The ordering must not be sensitive to that remainder.
        var almost = Membership(
            joinedAt: Early.AddTicks(10),
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"));

        var exact = Membership(
            joinedAt: Early,
            id: Guid.Parse("00000000-0000-0000-0000-0000000000ff"));

        Assert.Equal(
            exact.WorkspaceId,
            WorkspaceMembershipOrder
                .SelectSessionMembership([almost, exact])!
                .WorkspaceId);
    }

    // --------------------------------------------------------- the ordering

    [Fact]
    public void Ordering_is_oldest_first_across_a_mixed_set()
    {
        var memberships = new[]
        {
            Membership(joinedAt: Late, id: Guid.Parse("00000000-0000-0000-0000-000000000001")),
            Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-0000000000ff")),
            Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-00000000000a")),
            Membership(joinedAt: new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                id: Guid.Parse("00000000-0000-0000-0000-000000000042"))
        };

        var ordered = WorkspaceMembershipOrder
            .Apply(memberships)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal(
            new[]
            {
                // The two Early memberships first, lowest identifier between them.
                Guid.Parse("00000000-0000-0000-0000-00000000000a"),
                Guid.Parse("00000000-0000-0000-0000-0000000000ff"),
                Guid.Parse("00000000-0000-0000-0000-000000000042"),
                Guid.Parse("00000000-0000-0000-0000-000000000001")
            },
            ordered);
    }

    [Fact]
    public void Ordering_an_empty_set_yields_nothing()
    {
        Assert.Empty(WorkspaceMembershipOrder.Apply(
            Array.Empty<WorkspaceMember>()));
    }

    [Fact]
    public void Precedes_agrees_with_the_ordering_it_documents()
    {
        var older = Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-0000000000ff"));
        var newer = Membership(joinedAt: Late, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var lowerTied = Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higherTied = Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        Assert.True(WorkspaceMembershipOrder.Precedes(older, newer));
        Assert.False(WorkspaceMembershipOrder.Precedes(newer, older));

        Assert.True(WorkspaceMembershipOrder.Precedes(lowerTied, higherTied));
        Assert.False(WorkspaceMembershipOrder.Precedes(higherTied, lowerTied));

        // Not reflexive in a way that would let a caller conclude a membership
        // precedes itself and build an inconsistent ordering out of it.
        Assert.False(WorkspaceMembershipOrder.Precedes(older, older));
    }

    [Fact]
    public void An_identical_pair_has_no_order_between_its_members()
    {
        // Two rows that agree on both ordering columns. The rule cannot separate
        // them, and pretending otherwise would hide a real uniqueness problem
        // behind a claim of determinism.
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var first = Membership(joinedAt: Early, id: id);
        var second = Membership(joinedAt: Early, id: id);

        Assert.False(WorkspaceMembershipOrder.Precedes(first, second));
        Assert.False(WorkspaceMembershipOrder.Precedes(second, first));
    }

    // ------------------------------------------------------ queryable overload

    [Fact]
    public void The_queryable_overload_applies_the_same_rule_as_the_enumerable_one()
    {
        var memberships = new[]
        {
            Membership(joinedAt: Late, id: Guid.Parse("00000000-0000-0000-0000-000000000001")),
            Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-0000000000ff")),
            Membership(joinedAt: Early, id: Guid.Parse("00000000-0000-0000-0000-00000000000a"))
        };

        // Both overloads exist because the ordering has to survive translation to
        // SQL in production. If the two disagreed, the rule under test in a unit
        // test would not be the rule the database applies.
        var fromQuery = WorkspaceMembershipOrder
            .Apply(memberships.AsQueryable())
            .Select(x => x.Id)
            .ToArray();

        var fromEnumerable = WorkspaceMembershipOrder
            .Apply(memberships)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal(fromEnumerable, fromQuery);
    }

    [Fact]
    public void The_queryable_overload_leaves_the_caller_free_to_filter_first()
    {
        var mine = Membership(joinedAt: Early);
        var theirs = Membership(joinedAt: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        mine.UserId = User;
        theirs.UserId = User;
        // Make mine earlier
        mine.JoinedAt = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        theirs.JoinedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // The order composes after a filter, which is how login uses it: scope to
        // this user's rows, then take the first.
        var selected = WorkspaceMembershipOrder
            .Apply(new[] { mine, theirs }.AsQueryable()
                .Where(x => x.UserId == User))
            .First();

        Assert.Equal(mine.WorkspaceId, selected.WorkspaceId);
    }

    // ---------------------------------------------------------------- helpers

    private static WorkspaceMember Membership(
        DateTime joinedAt,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            UserId = User,
            Role = WorkspaceRole.Owner,
            JoinedAt = joinedAt
        };
}