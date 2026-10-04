using System.Net;
using Api.Configuration;
using Api.Contracts.Auth;
using Api.Contracts.Workspace;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Listing memberships, switching between them, and creating another workspace.
/// </summary>
/// <remarks>
/// <para>
/// Workspace context is a property of the session, not of the request. Every
/// test here therefore asserts on the credentials the next request will carry,
/// read out of the browser's own cookie jar, because a workspace id that
/// appeared in a response but never reached the session would leave the caller
/// still acting as whoever they were before.
/// </para>
/// <para>
/// The negative cases matter as much as the positive ones. A switch endpoint
/// that accepted any workspace identifier would be a total compromise of tenant
/// isolation, and it would do so quietly: the response would look correct.
/// </para>
/// </remarks>
public sealed class WorkspaceSessionTests : IAsyncLifetime
{
    private readonly MayorxApiFactory _factory = MayorxApiFactory.WithSettings(
        new Dictionary<string, string?>
        {
            ["RateLimiting:AuthenticationPermitLimit"] = "0"
        });

    private BrowserClient _browser = null!;

    public async Task InitializeAsync()
    {
        _factory.Initialise();
        await _factory.MigrateAsync();

        _browser = new BrowserClient(_factory);
    }

    public Task DisposeAsync()
    {
        _browser?.Dispose();
        _factory.Dispose();

        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------- list

    [Fact]
    public async Task Listing_workspaces_returns_the_callers_memberships()
    {
        var session = await TestSession.RegisterAsync(_browser, "list");

        var workspaces = await ReadWorkspacesAsync(_browser);

        var entry = Assert.Single(workspaces);

        Assert.Equal(session.WorkspaceId, entry.Id);
        Assert.Equal("Owner", entry.Role);
        Assert.Equal("Active", entry.Status);
        Assert.True(entry.IsCurrent);
        Assert.Equal("Workspace list", entry.Name);
    }

    [Fact]
    public async Task Listing_workspaces_never_returns_another_users_workspace()
    {
        var mine = await TestSession.RegisterAsync(_browser, "list-mine");

        using var stranger = new BrowserClient(_factory);
        var theirs = await TestSession.RegisterAsync(stranger, "list-theirs");

        var workspaces = await ReadWorkspacesAsync(_browser);

        Assert.Equal(mine.WorkspaceId, Assert.Single(workspaces).Id);
        Assert.DoesNotContain(
            workspaces,
            entry => entry.Id == theirs.WorkspaceId);
    }

    [Fact]
    public async Task Listing_workspaces_requires_a_session()
    {
        using var anonymous = new BrowserClient(_factory);

        using var response = await anonymous.GetAsync("api/v1/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Listing_workspaces_marks_exactly_one_entry_as_current()
    {
        var first = await TestSession.RegisterAsync(_browser, "list-current");

        await CreateWorkspaceAsync(_browser, "Second list-current");

        var workspaces = await ReadWorkspacesAsync(_browser);

        Assert.Equal(2, workspaces.Count);

        // Exactly one, because the session is scoped to exactly one workspace,
        // and it is the one the create switched to.
        var current = Assert.Single(workspaces, entry => entry.IsCurrent);

        Assert.NotEqual(first.WorkspaceId, current.Id);
    }

    // ----------------------------------------------------------------- create

    [Fact]
    public async Task Creating_a_workspace_makes_the_creator_its_owner()
    {
        var session = await TestSession.RegisterAsync(_browser, "create");

        using var response = await _browser.PostAsync(
            "api/v1/workspaces",
            new { name = "Agency Work" });

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = body.WorkspaceId;

        Assert.NotEqual(Guid.Empty, created);
        Assert.NotEqual(session.WorkspaceId, created);
        Assert.Equal(session.UserId, body.User.Id);

        // Owner, so the new workspace is administrable by the person who made
        // it. A workspace nobody could administer would be a dead end.
        var role = await RunAsync(context => context.WorkspaceMembers
            .AsNoTracking()
            .Where(x =>
                x.WorkspaceId == created &&
                x.UserId == session.UserId)
            .Select(x => x.Role)
            .SingleAsync());

        Assert.Equal(WorkspaceRole.Owner, role);

        var entry = Assert.Single(
            await ReadWorkspacesAsync(_browser),
            item => item.Id == created);

        Assert.Equal("Owner", entry.Role);
        Assert.Equal("Agency Work", entry.Name);
    }

    [Fact]
    public async Task Creating_a_workspace_makes_it_the_active_session()
    {
        var session = await TestSession.RegisterAsync(_browser, "create-active");

        using var response = await _browser.PostAsync(
            "api/v1/workspaces",
            new { name = "Active Immediately" });

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        // The session cookies, not just the response body. A create that reported
        // the new workspace while leaving the old credentials in the browser
        // would create data the user could then never see.
        Assert.Equal(
            body.WorkspaceId,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));

        using var current = await _browser.GetAsync(
            "api/v1/workspaces/current");

        var workspace = await TestHttp.ReadAsync<WorkspaceResponse>(current);

        TestHttp.EnsureSucceeded(current, workspace);

        Assert.Equal(body.WorkspaceId, workspace.Id);

        // And the previous workspace is still reachable, because the user is a
        // member of both.
        Assert.Contains(
            await ReadWorkspacesAsync(_browser),
            entry => entry.Id == session.WorkspaceId);
    }

    [Fact]
    public async Task Creating_a_workspace_without_a_name_is_refused()
    {
        await TestSession.RegisterAsync(_browser, "create-blank");

        using var response = await _browser.PostAsync(
            "api/v1/workspaces",
            new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_workspace_requires_a_session()
    {
        using var anonymous = new BrowserClient(_factory);

        using var response = await anonymous.PostAsync(
            "api/v1/workspaces",
            new { name = "Anonymous" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ----------------------------------------------------------------- switch

    [Fact]
    public async Task Switching_issues_a_session_bound_to_the_new_workspace()
    {
        var session = await TestSession.RegisterAsync(_browser, "switch");

        var second = await CreateWorkspaceAsync(_browser, "Switched To");

        using var response = await _browser.PostAsync(
            $"api/v1/workspaces/{second}/switch");

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        Assert.Equal(second, body.WorkspaceId);
        Assert.Equal(session.UserId, body.User.Id);

        // The claim in the new access token is what every later request is
        // authorised against, so it is read out of the browser's own jar rather
        // than inferred from the response.
        Assert.Equal(
            second,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));

        using var current = await _browser.GetAsync(
            "api/v1/workspaces/current");

        var workspace = await TestHttp.ReadAsync<WorkspaceResponse>(current);

        TestHttp.EnsureSucceeded(current, workspace);

        Assert.Equal(second, workspace.Id);
    }

    [Fact]
    public async Task Switching_binds_the_refresh_token_to_the_new_workspace()
    {
        var session = await TestSession.RegisterAsync(_browser, "switch-refresh");

        var second = await CreateWorkspaceAsync(_browser, "Switch Refresh");

        using var switched = await _browser.PostAsync(
            $"api/v1/workspaces/{second}/switch");

        TestHttp.EnsureSucceeded(switched);

        // The stored row, not the request: a refresh token redeemable against a
        // workspace it was not issued for would undo the switch on the next
        // renewal.
        //
        // Looked up by the hash of the token the browser is holding now, because
        // a workspace owns more than one refresh token over its life. Creating it
        // issued one and switching to it issued another, and neither is an error:
        // a switch deliberately leaves the session it replaced usable, the same as
        // a second login does. So the property under test is which workspace the
        // current credential belongs to, not how many exist.
        var stored = await RunAsync(context => context.RefreshTokens
            .AsNoTracking()
            .Where(token =>
                token.TokenHash ==
                Api.Application.Authentication.RefreshTokenService
                    .HashToken(_browser.RefreshToken))
            .Select(token => new { token.WorkspaceId, token.UserId, token.Status })
            .SingleAsync());

        Assert.Equal(second, stored.WorkspaceId);
        Assert.Equal(session.UserId, stored.UserId);
        Assert.Equal(RefreshTokenStatus.Active, stored.Status);
    }

    [Fact]
    public async Task Refreshing_after_a_switch_stays_in_the_new_workspace()
    {
        await TestSession.RegisterAsync(_browser, "switch-renew");

        var second = await CreateWorkspaceAsync(_browser, "Renew In");

        using var switched = await _browser.PostAsync(
            $"api/v1/workspaces/{second}/switch");
        TestHttp.EnsureSucceeded(switched);

        using var refreshed = await _browser.PostAsync("api/v1/auth/refresh");

        var body = await TestHttp.ReadAsync<AuthResponse>(refreshed);

        TestHttp.EnsureSucceeded(refreshed, body);

        Assert.Equal(second, body.WorkspaceId);
        Assert.Equal(
            second,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));
    }

    [Fact]
    public async Task Switching_to_a_workspace_the_caller_does_not_belong_to_is_refused()
    {
        var session = await TestSession.RegisterAsync(_browser, "switch-stranger");

        using var stranger = new BrowserClient(_factory);
        var theirs = await TestSession.RegisterAsync(stranger, "switch-owner");

        using var response = await _browser.PostAsync(
            $"api/v1/workspaces/{theirs.WorkspaceId}/switch");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "WORKSPACE_ACCESS_DENIED",
            await ReadErrorCodeAsync(response));

        // And the session is untouched. A refused switch that still moved the
        // caller would be worse than one that simply refused.
        Assert.Equal(
            session.WorkspaceId,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));

        Assert.DoesNotContain(
            await ReadWorkspacesAsync(_browser),
            entry => entry.Id == theirs.WorkspaceId);
    }

    [Fact]
    public async Task Switching_to_a_workspace_that_does_not_exist_is_refused()
    {
        await TestSession.RegisterAsync(_browser, "switch-missing");

        using var response = await _browser.PostAsync(
            $"api/v1/workspaces/{Guid.NewGuid()}/switch");

        // Refused as a membership failure rather than a 404: an authenticated
        // caller is not entitled to learn which workspace identifiers exist.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "WORKSPACE_ACCESS_DENIED",
            await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_missing_workspace_and_a_foreign_one_are_indistinguishable()
    {
        var mine = await TestSession.RegisterAsync(_browser, "switch-enum");

        using var stranger = new BrowserClient(_factory);
        var theirs = await TestSession.RegisterAsync(stranger, "switch-enum-owner");

        // Two identifiers: one that belongs to another tenant, one that was never
        // issued to anyone. The API resolves existence and membership in a single
        // query and answers both with the same status and the same error code.
        //
        // If they differed in any observable way, this endpoint would be an
        // enumeration oracle: an authenticated caller could walk the space of
        // GUIDs and learn which workspaces exist in the system by watching for a
        // 404, which is a disclosure about other tenants that tenant isolation is
        // supposed to prevent. Asserting the whole shape rather than the code
        // alone, so a future "helpful" 404 cannot come back unnoticed.
        using var foreign = await _browser.PostAsync(
            $"api/v1/workspaces/{theirs.WorkspaceId}/switch");

        using var missing = await _browser.PostAsync(
            $"api/v1/workspaces/{Guid.NewGuid()}/switch");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);

        Assert.Equal(
            "WORKSPACE_ACCESS_DENIED",
            await ReadErrorCodeAsync(foreign));
        Assert.Equal(
            "WORKSPACE_ACCESS_DENIED",
            await ReadErrorCodeAsync(missing));

        // The messages are identical too. A message that said "no such workspace"
        // for one case and "not a member" for the other would leak exactly as
        // much as the status code would.
        Assert.Equal(
            await ReadErrorMessageAsync(foreign),
            await ReadErrorMessageAsync(missing));

        // And the caller is left exactly where it was, in both cases. A refused
        // switch that still moved the session would be worse than one that simply
        // refused.
        Assert.Equal(
            mine.WorkspaceId,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));
    }

    [Fact]
    public async Task Switching_to_a_workspace_that_has_been_deleted_is_refused()
    {
        var session = await TestSession.RegisterAsync(_browser, "switch-deleted");

        var second = await CreateWorkspaceAsync(_browser, "Then Deleted");

        // Deleting a workspace cascades to its membership rows, so the caller's
        // entitlement disappears with the thing it was an entitlement to. The
        // session is not allowed to keep naming a workspace that is gone.
        //
        // Existence and membership are one predicate, so this needs no special
        // case: the row is simply not produced.
        await RunAsync<bool>(async context =>
        {
            var workspace = await context.Workspaces
                .SingleAsync(x => x.Id == second);

            context.Workspaces.Remove(workspace);

            await context.SaveChangesAsync();

            return true;
        });

        // The cascade really did remove the entitlement, so this is a refusal on
        // the merits rather than an accident of the delete.
        Assert.Empty(
            await RunAsync(context => context.WorkspaceMembers
                .AsNoTracking()
                .Where(x => x.WorkspaceId == second)
                .ToListAsync()));

        using var response = await _browser.PostAsync(
            $"api/v1/workspaces/{second}/switch");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "WORKSPACE_ACCESS_DENIED",
            await ReadErrorCodeAsync(response));

        // Still usable in the workspace it legitimately belongs to.
        using var current = await _browser.GetAsync("api/v1/workspaces/current");

        var workspace = await TestHttp.ReadAsync<WorkspaceResponse>(current);

        TestHttp.EnsureSucceeded(current, workspace);

        Assert.Equal(session.WorkspaceId, workspace.Id);
    }

    [Fact]
    public async Task Switching_does_not_invalidate_the_workspace_it_came_from()
    {
        var first = await TestSession.RegisterAsync(_browser, "switch-back");

        var second = await CreateWorkspaceAsync(_browser, "Kept Alive");

        using var away = await _browser.PostAsync(
            $"api/v1/workspaces/{second}/switch");
        TestHttp.EnsureSucceeded(away);

        // Two workspaces a user belongs to are not two logins, and neither may
        // log the other out.
        using var back = await _browser.PostAsync(
            $"api/v1/workspaces/{first.WorkspaceId}/switch");
        TestHttp.EnsureSucceeded(back);

        Assert.Equal(
            first.WorkspaceId,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));
    }

    [Fact]
    public async Task Switching_requires_a_session()
    {
        var session = await TestSession.RegisterAsync(_browser, "switch-anon");

        using var anonymous = new BrowserClient(_factory);

        using var response = await anonymous.PostAsync(
            $"api/v1/workspaces/{session.WorkspaceId}/switch");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task One_users_switch_never_changes_what_another_users_session_reads()
    {
        var owner = await TestSession.RegisterAsync(_browser, "iso-owner");

        using var stranger = new BrowserClient(_factory);
        await TestSession.RegisterAsync(stranger, "iso-stranger");

        var second = await CreateWorkspaceAsync(_browser, "Owner Only");

        using var refused = await stranger.PostAsync(
            $"api/v1/workspaces/{second}/switch");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // The stranger's session still points at its own workspace, not at the
        // one it just failed to reach.
        using var current = await stranger.GetAsync(
            "api/v1/workspaces/current");

        var workspace = await TestHttp.ReadAsync<WorkspaceResponse>(current);

        TestHttp.EnsureSucceeded(current, workspace);

        Assert.NotEqual(second, workspace.Id);
        Assert.NotEqual(owner.WorkspaceId, workspace.Id);
    }

    // ------------------------------------------------------- login selection

    [Fact]
    public async Task Logging_in_always_opens_the_workspace_joined_first()
    {
        var session = await TestSession.RegisterAsync(_browser, "login-order");

        // A second workspace, joined later, so the two memberships are genuinely
        // different rows with genuinely different join times. The session is now
        // in it, which is exactly the state that made the original rule a bug:
        // login ignored the choice and took whichever row came back first.
        var second = await CreateWorkspaceAsync(_browser, "Joined Later");

        Assert.Equal(
            second,
            JwtClaims.ReadWorkspaceId(_browser.AccessToken));

        var opened = new List<Guid>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            opened.Add(
                (await LoginAsync("login-order")).WorkspaceId);
        }

        Assert.Single(opened.Distinct());
        Assert.All(
            opened,
            workspaceId => Assert.Equal(session.WorkspaceId, workspaceId));
    }

    [Fact]
    public async Task A_user_with_one_workspace_still_lands_in_it()
    {
        var session = await TestSession.RegisterAsync(_browser, "login-single");

        var opened = new List<Guid>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            opened.Add((await LoginAsync("login-single")).WorkspaceId);
        }

        Assert.All(
            opened,
            workspaceId => Assert.Equal(session.WorkspaceId, workspaceId));
    }

    [Fact]
    public async Task Logging_in_is_stable_even_when_memberships_share_a_join_time()
    {
        var session = await TestSession.RegisterAsync(_browser, "login-tie");

        // Two memberships written with the same timestamp, which is what a bulk
        // import or two workspaces created inside one database transaction
        // produces. Ordering on the timestamp alone would leave the answer to the
        // database, so this is the case the identifier tie-break exists for.
        //
        // The timestamp is in the past, so both of these are genuinely older
        // than the membership registration created and therefore win outright.
        // Were it in the future the registration would be the oldest, and the
        // tie between these two would never come into play.
        var tied = await RunAsync(async context =>
        {
            var joinedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var members = new List<(Guid WorkspaceId, Guid MemberId)>();

            for (var index = 0; index < 2; index++)
            {
                var workspace = new Workspace
                {
                    Id = Guid.NewGuid(),
                    Name = $"Tied {index}",
                    Status = WorkspaceStatus.Active,
                    CreatedAt = joinedAt,
                    UpdatedAt = joinedAt
                };

                var memberId = Guid.NewGuid();

                context.Workspaces.Add(workspace);
                context.WorkspaceMembers.Add(new WorkspaceMember
                {
                    Id = memberId,
                    UserId = session.UserId,
                    WorkspaceId = workspace.Id,
                    Role = WorkspaceRole.Editor,
                    JoinedAt = joinedAt
                });

                members.Add((workspace.Id, memberId));
            }

            await context.SaveChangesAsync();

            return members;
        });

        var opened = new List<Guid>();

        for (var attempt = 0; attempt < 8; attempt++)
        {
            opened.Add((await LoginAsync("login-tie")).WorkspaceId);
        }

        // Every login lands in one workspace, not just most of them.
        Assert.Single(opened.Distinct());

        // And that workspace is the one the rule names: oldest membership first,
        // and on a tie the lowest membership identifier. Both tied memberships
        // are older than the registration, so the answer is one of these two and
        // has to be the same one every time rather than whichever row the
        // database happened to return first.
        //
        // The tie-break is on the membership's own identifier, not the
        // workspace's, so the expectation is built from the same column the API
        // orders by. Comparing workspace identifiers instead would pass or fail
        // by coincidence, since the two are assigned independently.
        Assert.Equal(
            tied.OrderBy(member => member.MemberId).First().WorkspaceId,
            opened[0]);
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<Guid> CreateWorkspaceAsync(
        BrowserClient browser,
        string name)
    {
        using var response = await browser.PostAsync(
            "api/v1/workspaces",
            new { name });

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        return body.WorkspaceId;
    }

    /// <summary>
    /// A fresh browser each time, so a login is not answered from the cookies an
    /// earlier one left behind.
    /// </summary>
    private async Task<AuthResponse> LoginAsync(string suffix)
    {
        using var browser = new BrowserClient(_factory);

        using var response = await browser.PostAsync(
            "api/v1/auth/login",
            new
            {
                email = $"owner-{suffix}@example.test",
                password = "Integration-Test-Password-42"
            });

        var body = await TestHttp.ReadAsync<AuthResponse>(response);

        TestHttp.EnsureSucceeded(response, body);

        return body;
    }

    private static async Task<List<WorkspaceMembershipResponse>>
        ReadWorkspacesAsync(BrowserClient browser)
    {
        using var response = await browser.GetAsync("api/v1/workspaces");

        var workspaces =
            await TestHttp.ReadAsync<List<WorkspaceMembershipResponse>>(response);

        TestHttp.EnsureSucceeded(response);

        return workspaces;
    }

    private static async Task<string> ReadErrorCodeAsync(
        HttpResponseMessage response)
    {
        return await ReadErrorFieldAsync(response, "code");
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response)
    {
        return await ReadErrorFieldAsync(response, "message");
    }

    private static async Task<string> ReadErrorFieldAsync(
        HttpResponseMessage response,
        string field)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = System.Text.Json.JsonDocument.Parse(body);

        return document.RootElement
            .GetProperty("error")
            .GetProperty(field)
            .GetString()!;
    }

    private async Task<T> RunAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        return await work(
            scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}