using Api.Domain.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<ContentIdea>(entity =>
        {
            entity.HasKey(x => x.Id);

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

            entity.HasOne(x => x.Workspace)
                .WithMany(x => x.ContentIdeas)
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