using Api.Application.Authentication;
using Api.Application.ContentPillars;
using Api.Contracts.ContentPillar;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using Xunit;

namespace Api.Tests;

public sealed class ContentPillarServiceTests
{
    // ---------------------------------------------------------------------
    // Creation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Create_stores_the_pillar_against_the_callers_workspace()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var result = await CreateService(dbContext, workspaceId).CreateAsync(
            new CreateContentPillarRequest { Name = "Education", Weight = 30m });

        Assert.Equal("Education", result.Name);
        Assert.Equal(30m, result.Weight);
        Assert.True(result.IsActive);
        Assert.Equal(0, result.IdeaCount);

        var stored = await dbContext.ContentPillars.SingleAsync();
        Assert.Equal(workspaceId, stored.WorkspaceId);
    }

    [Fact]
    public async Task Create_trims_whitespace_from_name_and_description()
    {
        await using var dbContext = CreateDbContext();

        var result = await CreateService(dbContext, Guid.NewGuid()).CreateAsync(
            new CreateContentPillarRequest
            {
                Name = "   Education   ",
                Description = "  Explain things simply.  ",
                Weight = 20m
            });

        Assert.Equal("Education", result.Name);
        Assert.Equal("Explain things simply.", result.Description);
    }

    [Fact]
    public async Task Create_rejects_a_missing_name()
    {
        await using var dbContext = CreateDbContext();

        await Assert.ThrowsAsync<InvalidContentPillarRequestException>(() =>
            CreateService(dbContext, Guid.NewGuid()).CreateAsync(
                new CreateContentPillarRequest { Name = "   ", Weight = 10m }));
    }

    [Fact]
    public async Task Create_rejects_a_name_longer_than_the_column()
    {
        await using var dbContext = CreateDbContext();

        await Assert.ThrowsAsync<InvalidContentPillarRequestException>(() =>
            CreateService(dbContext, Guid.NewGuid()).CreateAsync(
                new CreateContentPillarRequest { Name = new string('x', 151), Weight = 10m }));
    }

    [Fact]
    public async Task Create_requires_a_weight()
    {
        await using var dbContext = CreateDbContext();

        await Assert.ThrowsAsync<InvalidContentPillarRequestException>(() =>
            CreateService(dbContext, Guid.NewGuid()).CreateAsync(
                new CreateContentPillarRequest { Name = "Education" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    public async Task Create_rejects_a_weight_outside_the_documented_range(decimal weight)
    {
        await using var dbContext = CreateDbContext();

        await Assert.ThrowsAsync<InvalidContentPillarRequestException>(() =>
            CreateService(dbContext, Guid.NewGuid()).CreateAsync(
                new CreateContentPillarRequest { Name = "Education", Weight = weight }));
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_in_the_same_workspace()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var service = CreateService(dbContext, workspaceId);

        await service.CreateAsync(new CreateContentPillarRequest { Name = "Education", Weight = 10m });

        await Assert.ThrowsAsync<DuplicateContentPillarNameException>(() =>
            service.CreateAsync(new CreateContentPillarRequest { Name = "education", Weight = 20m }));
    }

    [Fact]
    public async Task The_same_name_is_allowed_in_a_different_workspace()
    {
        await using var dbContext = CreateDbContext();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await CreateService(dbContext, first)
            .CreateAsync(new CreateContentPillarRequest { Name = "Education", Weight = 10m });
        var result = await CreateService(dbContext, second)
            .CreateAsync(new CreateContentPillarRequest { Name = "Education", Weight = 10m });

        Assert.Equal("Education", result.Name);
    }

    [Fact]
    public async Task Create_rejects_an_audience_profile_from_another_workspace()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var audienceId = Guid.NewGuid();

        dbContext.AudienceProfiles.Add(new AudienceProfile
        {
            Id = audienceId,
            WorkspaceId = otherWorkspaceId,
            Description = "Developers"
        });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ContentPillarAudienceNotFoundException>(() =>
            CreateService(dbContext, workspaceId).CreateAsync(
                new CreateContentPillarRequest
                {
                    Name = "Education",
                    Weight = 10m,
                    AudienceProfileId = audienceId
                }));
    }

    [Fact]
    public async Task Create_accepts_an_audience_profile_from_the_same_workspace()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var audienceId = Guid.NewGuid();

        dbContext.AudienceProfiles.Add(new AudienceProfile
        {
            Id = audienceId,
            WorkspaceId = workspaceId,
            Description = "Developers"
        });
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, workspaceId).CreateAsync(
            new CreateContentPillarRequest
            {
                Name = "Education",
                Weight = 10m,
                AudienceProfileId = audienceId
            });

        Assert.Equal(audienceId, result.AudienceProfileId);
    }

    // ---------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------

    [Fact]
    public async Task List_never_returns_another_workspaces_pillars()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();

        SeedPillar(dbContext, workspaceId, "Education");
        SeedPillar(dbContext, otherWorkspaceId, "Secret");

        var result = await CreateService(dbContext, workspaceId).GetAsync(null);

        Assert.Single(result);
        Assert.Equal("Education", result[0].Name);
    }

    [Fact]
    public async Task List_orders_by_weight_heaviest_first_and_breaks_ties_by_name()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedPillar(dbContext, workspaceId, "Stories", weight: 20m);
        SeedPillar(dbContext, workspaceId, "Education", weight: 20m);
        SeedPillar(dbContext, workspaceId, "Product", weight: 50m);

        var result = await CreateService(dbContext, workspaceId).GetAsync(null);

        Assert.Equal(["Product", "Education", "Stories"], result.Select(p => p.Name));
    }

    [Fact]
    public async Task List_filters_on_is_active()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedPillar(dbContext, workspaceId, "Education");
        var retired = SeedPillar(dbContext, workspaceId, "Old");
        retired.IsActive = false;
        await dbContext.SaveChangesAsync();

        var active = await CreateService(dbContext, workspaceId).GetAsync(true);
        var inactive = await CreateService(dbContext, workspaceId).GetAsync(false);

        Assert.Equal("Education", Assert.Single(active).Name);
        Assert.Equal("Old", Assert.Single(inactive).Name);
    }

    [Fact]
    public async Task List_reports_how_many_ideas_are_attributed_to_each_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillarId = SeedPillar(dbContext, workspaceId, "Education").Id;

        SeedIdea(dbContext, workspaceId, pillarId);
        SeedIdea(dbContext, workspaceId, pillarId);
        SeedIdea(dbContext, workspaceId, null);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, workspaceId).GetAsync(null);

        Assert.Equal(2, Assert.Single(result).IdeaCount);
    }

    [Fact]
    public async Task Get_by_id_does_not_leak_another_workspaces_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var foreign = SeedPillar(dbContext, otherWorkspaceId, "Secret");

        var result = await CreateService(dbContext, workspaceId).GetAsync(foreign.Id);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------
    // Updates
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Update_changes_only_the_fields_that_were_supplied()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedPillar(dbContext, workspaceId, "Education", weight: 10m);

        var result = (await CreateService(dbContext, workspaceId).UpdateAsync(
            SinglePillarId(dbContext),
            new UpdateContentPillarRequest { Weight = 55m }))!;

        Assert.Equal(55m, result.Weight);
        Assert.Equal("Education", result.Name);
    }

    [Fact]
    public async Task Update_moves_the_updated_at_timestamp()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillar = SeedPillar(dbContext, workspaceId, "Education");
        var original = pillar.UpdatedAt;

        var result = (await CreateService(dbContext, workspaceId).UpdateAsync(
            pillar.Id,
            new UpdateContentPillarRequest { Name = "Learning" }))!;

        Assert.NotEqual(original, result.UpdatedAt);
    }

    [Fact]
    public async Task Update_can_deactivate_without_touching_the_name()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedPillar(dbContext, workspaceId, "Education");

        var result = (await CreateService(dbContext, workspaceId).UpdateAsync(
            SinglePillarId(dbContext),
            new UpdateContentPillarRequest { IsActive = false }))!;

        Assert.False(result.IsActive);
        Assert.Equal("Education", result.Name);
    }

    [Fact]
    public async Task Update_rejects_renaming_onto_another_pillars_name()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedPillar(dbContext, workspaceId, "Education");
        var other = SeedPillar(dbContext, workspaceId, "Stories");

        await Assert.ThrowsAsync<DuplicateContentPillarNameException>(() =>
            CreateService(dbContext, workspaceId).UpdateAsync(
                other.Id,
                new UpdateContentPillarRequest { Name = "education" }));
    }

    [Fact]
    public async Task Update_allows_keeping_the_same_name()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillar = SeedPillar(dbContext, workspaceId, "Education");

        var result = (await CreateService(dbContext, workspaceId).UpdateAsync(
            pillar.Id,
            new UpdateContentPillarRequest { Name = "Education", Weight = 30m }))!;

        Assert.Equal(30m, result.Weight);
    }

    [Fact]
    public async Task Update_returns_null_for_another_workspaces_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var foreign = SeedPillar(dbContext, Guid.NewGuid(), "Secret");

        var result = await CreateService(dbContext, workspaceId).UpdateAsync(
            foreign.Id,
            new UpdateContentPillarRequest { Name = "Stolen" });

        Assert.Null(result);
    }

    [Fact]
    public async Task Update_rejects_an_out_of_range_weight()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillar = SeedPillar(dbContext, workspaceId, "Education");

        await Assert.ThrowsAsync<InvalidContentPillarRequestException>(() =>
            CreateService(dbContext, workspaceId).UpdateAsync(
                pillar.Id,
                new UpdateContentPillarRequest { Weight = 0m }));
    }

    // ---------------------------------------------------------------------
    // Deletion
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Delete_removes_the_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillar = SeedPillar(dbContext, workspaceId, "Education");

        var deleted = await CreateService(dbContext, workspaceId).DeleteAsync(pillar.Id);

        Assert.True(deleted);
        Assert.Empty(await dbContext.ContentPillars.ToListAsync());
    }

    [Fact]
    public async Task Delete_does_not_leak_when_another_workspace_owns_the_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var foreign = SeedPillar(dbContext, Guid.NewGuid(), "Secret");

        var deleted = await CreateService(dbContext, workspaceId).DeleteAsync(foreign.Id);

        Assert.False(deleted);
        Assert.Single(await dbContext.ContentPillars.ToListAsync());
    }

    [Fact]
    public async Task Delete_keeps_attributed_ideas_and_clears_their_pillar()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var pillar = SeedPillar(dbContext, workspaceId, "Education");
        var idea = SeedIdea(dbContext, workspaceId, pillar.Id);

        var deleted = await CreateService(dbContext, workspaceId).DeleteAsync(pillar.Id);

        Assert.True(deleted);

        var survivingIdea = await dbContext.ContentIdeas.SingleAsync();
        Assert.Equal(idea.Id, survivingIdea.Id);
        Assert.Null(survivingIdea.ContentPillarId);
    }

    [Fact]
    public async Task Delete_reports_false_for_a_pillar_that_is_already_gone()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        var deleted = await CreateService(dbContext, workspaceId).DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The delete path opens an explicit transaction, which PostgreSQL
            // needs and the in-memory store cannot provide. Suppress the warning
            // so the same production code path runs under test.
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ContentPillarService CreateService(AppDbContext dbContext, Guid workspaceId) =>
        new(dbContext, CreateCurrentUser(workspaceId));

    private static CurrentUserService CreateCurrentUser(Guid workspaceId)
    {
        var claims = new List<Claim>
        {
            new("workspace_id", workspaceId.ToString())
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };

        return new CurrentUserService(accessor);
    }

    private static ContentPillar SeedPillar(
        AppDbContext dbContext,
        Guid workspaceId,
        string name,
        decimal weight = 25m)
    {
        var pillar = new ContentPillar
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Description = string.Empty,
            Weight = weight,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.ContentPillars.Add(pillar);
        dbContext.SaveChanges();
        return pillar;
    }

    private static ContentIdea SeedIdea(AppDbContext dbContext, Guid workspaceId, Guid? pillarId)
    {
        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentPillarId = pillarId,
            Title = "An idea",
            Topic = "Topic",
            Angle = "Angle",
            Hook = "Hook",
            Reason = "Reason",
            SuggestedHashtags = "#tag",
            Status = ContentIdeaStatus.Suggested,
            Platform = ContentPlatform.LinkedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.ContentIdeas.Add(idea);
        dbContext.SaveChanges();
        return idea;
    }

    private static Guid SinglePillarId(AppDbContext dbContext)
    {
        dbContext.SaveChanges();
        return dbContext.ContentPillars.Single().Id;
    }
}
