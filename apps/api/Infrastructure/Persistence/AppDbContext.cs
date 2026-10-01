using Api.Domain.Entities;
using Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<BrandProfile> BrandProfiles => Set<BrandProfile>();
    public DbSet<AudienceProfile> AudienceProfiles => Set<AudienceProfile>();
    public DbSet<ContentStrategy> ContentStrategies => Set<ContentStrategy>();
    public DbSet<ContentIdea> ContentIdeas => Set<ContentIdea>();
    public DbSet<ContentPost> ContentPosts => Set<ContentPost>();
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();
    public DbSet<ContentPostMetric> ContentPostMetrics => Set<ContentPostMetric>();
    public DbSet<ContentPillar> ContentPillars => Set<ContentPillar>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AiRun> AiRuns => Set<AiRun>();
    public DbSet<LearningSignal> LearningSignals => Set<LearningSignal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<ContentIdea>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasAlternateKey(x => new { x.WorkspaceId, x.Id });

            entity.HasIndex(x => new
            {
                x.WorkspaceId,
                x.Status
            });

            entity.HasIndex(x => new
            {
                x.WorkspaceId,
                x.OverallScore
            });

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Platform)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.Topic)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.Angle)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.Hook)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.Reason)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(x => x.RelevanceScore)
                .HasPrecision(5, 2);

            entity.Property(x => x.EngagementPotentialScore)
                .HasPrecision(5, 2);

            entity.Property(x => x.BrandFitScore)
                .HasPrecision(5, 2);

            entity.Property(x => x.OverallScore)
                .HasPrecision(5, 2);

            // Serves the documented pillarId filter on the ideas list.
            entity.HasIndex(x => new { x.WorkspaceId, x.ContentPillarId });

            entity.HasOne(x => x.Workspace)
                .WithMany(x => x.ContentIdeas)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a pillar must never delete content: the idea survives and
            // becomes unattributed. The service detaches referencing ideas
            // before deleting the pillar, and Restrict then guarantees nothing
            // can bypass that. SetNull is not usable here — it would also try to
            // null the non-nullable WorkspaceId part of this composite key and
            // fail the whole delete.
            entity.HasOne(x => x.ContentPillar)
                .WithMany(x => x.ContentIdeas)
                .HasForeignKey(x => new { x.WorkspaceId, x.ContentPillarId })
                .HasPrincipalKey(x => new { x.WorkspaceId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContentPost>(entity =>
        {
            entity.HasKey(x => x.Id);

            // Lets child rows reference a post by (workspace, id) so the
            // database itself prevents a cross-workspace reference.
            entity.HasAlternateKey(x => new { x.WorkspaceId, x.Id });

            entity.HasIndex(x => new { x.ContentIdeaId, x.Platform })
                .IsUnique();

            entity.HasIndex(x => new { x.WorkspaceId, x.Status, x.CreatedAt });

            entity.HasIndex(x => new { x.Status, x.ScheduledAt });

            // The metrics scan filters on published posts and an attempt
            // timestamp, so this index serves the sync poll directly.
            entity.HasIndex(x => new { x.Status, x.MetricsLastAttemptedAt });

            entity.Property(x => x.Platform)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Content)
                .HasMaxLength(3000)
                .IsRequired();

            // A platform URN, not a URL. 255 covers every id the supported
            // platforms return without guessing at a longer bound.
            entity.Property(x => x.PublishedExternalPostId)
                .HasMaxLength(255);

            entity.Property(x => x.Hook)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.CallToAction)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.Hashtags)
                .HasColumnType("text[]")
                .IsRequired();

            entity.Property(x => x.ContentType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.StorytellingStructure)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.PublishFailureReason)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasOne(x => x.ContentIdea)
                .WithMany(x => x.ContentPosts)
                .HasForeignKey(x => new { x.WorkspaceId, x.ContentIdeaId })
                .HasPrincipalKey(x => new { x.WorkspaceId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContentPillar>(entity =>
        {
            entity.HasKey(x => x.Id);

            // Lets an idea reference a pillar by (workspace, id) so the database
            // itself prevents a cross-workspace attribution.
            entity.HasAlternateKey(x => new { x.WorkspaceId, x.Id });

            // A pillar is referenced by name in prompts and shown in a UI
            // picker, so two pillars with the same name in one workspace would
            // be ambiguous. Names are compared case-insensitively.
            entity.HasIndex(x => new { x.WorkspaceId, x.Name })
                .IsUnique();

            entity.HasIndex(x => new { x.WorkspaceId, x.IsActive });

            entity.Property(x => x.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.Weight)
                .HasPrecision(5, 2)
                .IsRequired();

            entity.HasOne(x => x.Workspace)
                .WithMany(x => x.ContentPillars)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);

            // audit_logs_workspace_created_at: the read endpoint always filters
            // to one workspace and orders newest first.
            entity.HasIndex(x => new { x.WorkspaceId, x.CreatedAt });

            // audit_logs_entity: "show me everything that happened to this record".
            entity.HasIndex(x => new { x.EntityType, x.EntityId });

            entity.Property(x => x.Action)
                .HasConversion<string>()
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.EntityType)
                .HasMaxLength(100)
                .IsRequired();

            // The schema types these as JSONB rather than text, so a payload
            // can be queried as JSON later instead of only parsed as a string.
            entity.Property(x => x.OldValues)
                .HasColumnType("jsonb");

            entity.Property(x => x.NewValues)
                .HasColumnType("jsonb");

            // INET rather than text, so address filtering and containment
            // checks stay possible without a cast.
            entity.Property(x => x.IpAddress)
                .HasColumnType("inet");

            // Restrict, not Cascade: the design says deleting a workspace must
            // not silently discard its audit history. Nothing updates these
            // rows, and there is no delete path, so the log is append-only.
            entity.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Set to null rather than restricted: deleting a user must not
            // delete the record of what they did.
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AiRun>(entity =>
        {
            entity.HasKey(x => x.Id);

            // ai_runs_workspace_created_at: runs are always scoped to one
            // workspace and read back as a time series.
            entity.HasIndex(x => new { x.WorkspaceId, x.CreatedAt });

            // ai_runs_workspace_run_type: "how much of each kind of AI work did
            // this workspace ask for, and how often did it fail".
            entity.HasIndex(x => new { x.WorkspaceId, x.RunType });

            entity.Property(x => x.RunType)
                .HasConversion(
                    value => AIRunTypeStorage.ToStorage(value),
                    value => AIRunTypeStorage.FromStorage(value))
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion(
                    value => AIRunStatusStorage.ToStorage(value),
                    value => AIRunStatusStorage.FromStorage(value))
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Provider)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Model)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.PromptVersion)
                .HasMaxLength(50);

            // The schema's DECIMAL(12,6). The provider default would be
            // decimal(18,2), which would not fit a cost in whole cents.
            entity.Property(x => x.EstimatedCost)
                .HasPrecision(12, 6);

            // JSONB rather than text, so metadata can be queried as JSON.
            entity.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            // Restrict, matching the other diagnostic tables: deleting a
            // workspace must not silently discard its AI history.
            entity.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LearningSignal>(entity =>
        {
            entity.HasKey(x => x.Id);

            // learning_signals_workspace_created_at: the read endpoint always
            // filters to one workspace and orders newest first. One index serves
            // both, matching the audit log and AI run tables. There is no unique
            // key: signals accumulate as the performance history grows, and a
            // repeated run over a longer window is a new observation rather than
            // a correction of an earlier one.
            entity.HasIndex(x => new { x.WorkspaceId, x.CreatedAt });

            entity.Property(x => x.SourceType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.SignalType)
                .HasMaxLength(100)
                .IsRequired();

            // The schema's DECIMAL(5,2), so a confidence value is 0.00 to 100.00.
            entity.Property(x => x.Confidence)
                .HasPrecision(5, 2);

            // JSONB rather than text, so the evidence behind a signal can be
            // queried as JSON instead of only parsed as a string.
            entity.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            // Restrict, matching the other diagnostic and learning tables:
            // deleting a workspace must not silently discard the observations
            // drawn from its performance history. The navigation is named
            // explicitly so this reconfigures the relationship EF discovers from
            // the Workspace property instead of adding a second one beside it.
            entity.HasOne(signal => signal.Workspace)
                .WithMany()
                .HasForeignKey(signal => signal.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContentPostMetric>(entity =>
        {
            entity.HasKey(x => x.Id);

            // A post may have several snapshots, so this is not unique. The
            // index serves both the snapshot lookup and the "latest per post"
            // read, which orders by RecordedAt descending.
            entity.HasIndex(x => new { x.ContentPostId, x.RecordedAt });

            entity.HasIndex(x => new { x.WorkspaceId, x.RecordedAt });

            // PostgreSQL does not index the referencing side of a foreign key
            // automatically. The composite key below joins on (WorkspaceId,
            // ContentPostId), so this index has to be declared in the model or
            // the next generated migration would drop the one the schema has.
            entity.HasIndex(x => new { x.WorkspaceId, x.ContentPostId });

            entity.HasOne(x => x.ContentPost)
                .WithMany(x => x.Metrics)
                .HasForeignKey(x => new { x.WorkspaceId, x.ContentPostId })
                .HasPrincipalKey(x => new { x.WorkspaceId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SocialAccount>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.WorkspaceId,
                x.Platform,
                x.PlatformAccountId
            })
                .IsUnique();

            entity.HasIndex(x => new { x.WorkspaceId, x.Status });

            entity.Property(x => x.Platform)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.PlatformAccountId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Username)
                .HasMaxLength(200);

            entity.Property(x => x.DisplayName)
                .HasMaxLength(200);

            entity.Property(x => x.ProfileUrl)
                .HasMaxLength(2000);

            entity.Property(x => x.AccessTokenEncrypted)
                .HasMaxLength(8000);

            entity.Property(x => x.RefreshTokenEncrypted)
                .HasMaxLength(8000);

            entity.Property(x => x.Scopes)
                .HasColumnType("text[]")
                .IsRequired();

            entity.HasOne(x => x.Workspace)
                .WithMany(x => x.SocialAccounts)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContentStrategy>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.WorkspaceId)
                .IsUnique();

            entity.Property(x => x.PostsPerWeek)
                .IsRequired();

            entity.Property(x => x.ContentPillars)
                .IsRequired();

            entity.Property(x => x.ContentObjectives)
                .IsRequired();

            entity.Property(x => x.PreferredFormats)
                .IsRequired();

            entity.Property(x => x.StorytellingStyle)
                .IsRequired();

            entity.Property(x => x.HashtagStrategy)
                .IsRequired();

            entity.Property(x => x.TopicsToPrioritize)
                .IsRequired();

            entity.Property(x => x.TopicsToAvoid)
                .IsRequired();

            entity.Property(x => x.PreferredPostingTimes)
                .IsRequired();

            entity.HasOne(x => x.Workspace)
                .WithOne(x => x.ContentStrategy)
                .HasForeignKey<ContentStrategy>(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.Property(x => x.Email)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.LastName)
                .HasMaxLength(100)
                .IsRequired();
        });

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
        });

        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.WorkspaceId,
                x.UserId
            })
            .IsUnique();

            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.HasOne(x => x.Workspace)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithMany(x => x.WorkspaceMemberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BrandProfile>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.WorkspaceId)
                .IsUnique();

            entity.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasOne(x => x.Workspace)
                .WithOne(x => x.BrandProfile)
                .HasForeignKey<BrandProfile>(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AudienceProfile>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.WorkspaceId)
                .IsUnique();

            entity.HasOne(x => x.Workspace)
                .WithOne(x => x.AudienceProfile)
                .HasForeignKey<AudienceProfile>(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}