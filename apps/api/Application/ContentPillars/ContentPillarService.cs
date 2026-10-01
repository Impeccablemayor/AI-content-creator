using Api.Application.Authentication;
using Api.Contracts.ContentPillar;
using Api.Domain.Entities;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Application.ContentPillars;

public interface IContentPillarService
{
    Task<IReadOnlyList<ContentPillarResponse>> GetAsync(
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<ContentPillarResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentPillarResponse> CreateAsync(
        CreateContentPillarRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentPillarResponse?> UpdateAsync(
        Guid id,
        UpdateContentPillarRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public sealed class ContentPillarService : IContentPillarService
{
    private const int MaxNameLength = 150;

    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;

    public ContentPillarService(AppDbContext dbContext, CurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ContentPillarResponse>> GetAsync(
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var query = _dbContext.ContentPillars.AsNoTracking()
            .Where(pillar => pillar.WorkspaceId == workspaceId);

        if (isActive.HasValue)
        {
            query = query.Where(pillar => pillar.IsActive == isActive.Value);
        }

        // Heaviest pillar first, because that is the order a content planner
        // wants to see. Name then id break ties so the list is stable.
        var pillars = await query
            .OrderByDescending(pillar => pillar.Weight)
            .ThenBy(pillar => pillar.Name)
            .ThenBy(pillar => pillar.Id)
            .ToListAsync(cancellationToken);

        var counts = await CountIdeasAsync(workspaceId, pillars, cancellationToken);

        return pillars
            .Select(pillar => Map(pillar, counts.GetValueOrDefault(pillar.Id)))
            .ToList();
    }

    public async Task<ContentPillarResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var pillar = await _dbContext.ContentPillars.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.WorkspaceId == workspaceId,
                cancellationToken);

        return pillar is null ? null : Map(pillar, await CountIdeasAsync(workspaceId, pillar.Id, cancellationToken));
    }

    public async Task<ContentPillarResponse> CreateAsync(
        CreateContentPillarRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspaceId = _currentUser.WorkspaceId;
        var name = NormalizeName(request.Name);

        if (request.Weight is not { } weight)
        {
            throw new InvalidContentPillarRequestException("Weight is required.");
        }

        ValidateWeight(weight);

        if (request.AudienceProfileId is { } audienceId)
        {
            await EnsureAudienceBelongsToWorkspaceAsync(workspaceId, audienceId, cancellationToken);
        }

        await EnsureNameIsFreeAsync(workspaceId, name, null, cancellationToken);

        var now = DateTime.UtcNow;
        var pillar = new ContentPillar
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Description = NormalizeOptional(request.Description) ?? string.Empty,
            Objective = NormalizeOptional(request.Objective),
            TargetAudienceId = request.AudienceProfileId,
            Weight = weight,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.ContentPillars.Add(pillar);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateName(exception))
        {
            // Two concurrent creates can both pass the check above; the unique
            // index is what actually guarantees it.
            throw new DuplicateContentPillarNameException(name);
        }

        return Map(pillar, 0);
    }

    public async Task<ContentPillarResponse?> UpdateAsync(
        Guid id,
        UpdateContentPillarRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspaceId = _currentUser.WorkspaceId;
        var pillar = await _dbContext.ContentPillars
            .SingleOrDefaultAsync(
                item => item.Id == id && item.WorkspaceId == workspaceId,
                cancellationToken);

        if (pillar is null)
        {
            return null;
        }

        if (request.Name is not null)
        {
            var name = NormalizeName(request.Name);
            await EnsureNameIsFreeAsync(workspaceId, name, id, cancellationToken);
            pillar.Name = name;
        }

        if (request.Description is not null)
        {
            pillar.Description = NormalizeOptional(request.Description) ?? string.Empty;
        }

        if (request.Objective is not null)
        {
            pillar.Objective = NormalizeOptional(request.Objective);
        }

        if (request.AudienceProfileId is { } audienceId)
        {
            if (audienceId != pillar.TargetAudienceId)
            {
                await EnsureAudienceBelongsToWorkspaceAsync(workspaceId, audienceId, cancellationToken);
            }

            pillar.TargetAudienceId = audienceId;
        }

        if (request.Weight is { } weight)
        {
            ValidateWeight(weight);
            pillar.Weight = weight;
        }

        if (request.IsActive is { } isActive)
        {
            pillar.IsActive = isActive;
        }

        pillar.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateName(exception))
        {
            throw new DuplicateContentPillarNameException(pillar.Name);
        }

        return Map(pillar, await CountIdeasAsync(workspaceId, pillar.Id, cancellationToken));
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var pillar = await _dbContext.ContentPillars
            .SingleOrDefaultAsync(
                item => item.Id == id && item.WorkspaceId == workspaceId,
                cancellationToken);

        if (pillar is null)
        {
            return false;
        }

        // Detach the pillar's ideas before removing it, so deleting a pillar
        // never deletes the content built from those ideas. The database
        // enforces this too: the relationship is Restrict, so a delete that
        // still had references would be refused rather than cascading.
        // Both steps share one transaction so a failure cannot leave the ideas
        // detached while the pillar survives.
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var referencing = await _dbContext.ContentIdeas
            .Where(idea => idea.WorkspaceId == workspaceId &&
                idea.ContentPillarId == pillar.Id)
            .ToListAsync(cancellationToken);

        foreach (var idea in referencing)
        {
            idea.ContentPillarId = null;
        }

        if (referencing.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _dbContext.ContentPillars.Remove(pillar);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private static string NormalizeName(string? name)
    {
        var normalized = name?.Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            throw new InvalidContentPillarRequestException("Name is required.");
        }

        if (normalized.Length > MaxNameLength)
        {
            throw new InvalidContentPillarRequestException(
                $"Name must be {MaxNameLength} characters or fewer.");
        }

        return normalized;
    }

    private static void ValidateWeight(decimal weight)
    {
        if (weight <= 0 || weight > 100)
        {
            throw new InvalidContentPillarRequestException(
                "Weight must be greater than 0 and at most 100.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private async Task EnsureNameIsFreeAsync(
        Guid workspaceId,
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var taken = await _dbContext.ContentPillars
            .AsNoTracking()
            .AnyAsync(
                pillar => pillar.WorkspaceId == workspaceId &&
                    pillar.Id != (excludingId ?? Guid.Empty) &&
                    pillar.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (taken)
        {
            throw new DuplicateContentPillarNameException(name);
        }
    }

    private async Task EnsureAudienceBelongsToWorkspaceAsync(
        Guid workspaceId,
        Guid audienceId,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.AudienceProfiles
            .AsNoTracking()
            .AnyAsync(
                profile => profile.Id == audienceId &&
                    profile.WorkspaceId == workspaceId,
                cancellationToken);

        if (!exists)
        {
            throw new ContentPillarAudienceNotFoundException();
        }
    }

    private async Task<Dictionary<Guid, int>> CountIdeasAsync(
        Guid workspaceId,
        IReadOnlyCollection<ContentPillar> pillars,
        CancellationToken cancellationToken)
    {
        var ids = pillars.Select(pillar => pillar.Id).ToList();

        var counts = await _dbContext.ContentIdeas
            .AsNoTracking()
            .Where(idea => idea.WorkspaceId == workspaceId &&
                idea.ContentPillarId != null &&
                ids.Contains(idea.ContentPillarId.Value))
            .GroupBy(idea => idea.ContentPillarId!.Value)
            .Select(group => new { PillarId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.PillarId, entry => entry.Count);
    }

    private Task<int> CountIdeasAsync(
        Guid workspaceId,
        Guid pillarId,
        CancellationToken cancellationToken) =>
        _dbContext.ContentIdeas
            .AsNoTracking()
            .CountAsync(
                idea => idea.WorkspaceId == workspaceId && idea.ContentPillarId == pillarId,
                cancellationToken);

    private static bool IsDuplicateName(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "IX_ContentPillars_WorkspaceId_Name")
            {
                return true;
            }
        }

        return false;
    }

    private static ContentPillarResponse Map(ContentPillar pillar, int ideaCount) => new()
    {
        Id = pillar.Id,
        Name = pillar.Name,
        Description = pillar.Description,
        Objective = pillar.Objective,
        AudienceProfileId = pillar.TargetAudienceId,
        Weight = pillar.Weight,
        IsActive = pillar.IsActive,
        IdeaCount = ideaCount,
        CreatedAt = pillar.CreatedAt,
        UpdatedAt = pillar.UpdatedAt
    };
}
