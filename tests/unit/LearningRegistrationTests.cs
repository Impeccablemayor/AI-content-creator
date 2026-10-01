using Api.Application.AI;
using Api.Application.Analytics;
using Api.Application.Authentication;
using Api.Application.Learning;
using Api.Configuration;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The learning loop is registered from configuration, so a bad threshold would
/// otherwise surface as a wrong comparison rather than as a startup failure.
/// </summary>
public sealed class LearningRegistrationTests
{
    [Fact]
    public void The_service_and_its_dependencies_resolve()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<IAiProvider>(new UnavailableAiProvider());
        services.AddSingleton<IAiRunRecorder, NoOpAiRunRecorder>();
        services.AddSingleton(new CurrentUserService(
            AuditLogTestContext.CreateAccessor(Guid.NewGuid())));
        services.AddScoped<IPublishedPostMetricsReader, PublishedPostMetricsReader>();
        services.AddLearning(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILearningService>());
    }

    [Fact]
    public void The_defaults_make_the_loop_say_less_rather_than_more()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLearning(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LearningLoopOptions>>().Value;

        Assert.Equal(90, options.LookbackDays);
        Assert.Equal(5, options.MinimumMeasuredPosts);
        Assert.Equal(2, options.MinimumPostsPerGroup);
        Assert.Equal(0.25m, options.MinimumEngagementRateGap);
    }

    [Theory]
    [InlineData("LearningLoop:LookbackDays", "0")]
    [InlineData("LearningLoop:MinimumMeasuredPosts", "0")]
    [InlineData("LearningLoop:MinimumPostsPerGroup", "0")]
    [InlineData("LearningLoop:MinimumEngagementRateGap", "-0.1")]
    public void An_out_of_range_threshold_fails_validation(string key, string value)
    {
        var settings = CreateConfiguration().AsEnumerable()
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        settings[key] = value;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLearning(new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build());

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LearningLoopOptions>>().Value);
    }

    /// <summary>
    /// A group minimum above the workspace minimum is not a stricter setting, it
    /// is an impossible one: no run could ever produce a comparison.
    /// </summary>
    [Fact]
    public void A_group_minimum_above_the_measured_minimum_fails_validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLearning(CreateConfiguration(
            minimumMeasuredPosts: "5",
            minimumPostsPerGroup: "10"));

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LearningLoopOptions>>().Value);

        Assert.Contains("no group could ever be compared", exception.Message, StringComparison.Ordinal);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration CreateConfiguration(
        string minimumMeasuredPosts = "5",
        string minimumPostsPerGroup = "2") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LearningLoop:LookbackDays"] = "90",
                ["LearningLoop:MinimumMeasuredPosts"] = minimumMeasuredPosts,
                ["LearningLoop:MinimumPostsPerGroup"] = minimumPostsPerGroup,
                ["LearningLoop:MinimumEngagementRateGap"] = "0.25"
            })
            .Build();

    /// <summary>
    /// A recorder that writes nothing, so resolving the graph does not need a
    /// second context. The recording path itself is covered by the service tests,
    /// which use the real recorder against the same database.
    /// </summary>
    private sealed class NoOpAiRunRecorder : IAiRunRecorder
    {
        public Task RecordSuccessAsync(
            AIRunType runType,
            string promptVersion,
            TimeSpan duration,
            object? metadata = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RecordFailureAsync(
            AIRunType runType,
            string promptVersion,
            TimeSpan duration,
            Exception exception,
            object? metadata = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
