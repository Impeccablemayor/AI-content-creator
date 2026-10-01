using Api.Application.AI.Prompts;
using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Application.Learning;

public static class LearningRegistration
{
    /// <summary>
    /// Registers the learning loop for the API.
    /// </summary>
    /// <remarks>
    /// The API host only, not the worker. A run makes AI calls, and AI run rows
    /// are recorded against the workspace of the request that triggered them —
    /// neither of which a background poll has. A run is triggered by the publisher
    /// asking for one, which is also the only moment the result is wanted.
    /// </remarks>
    public static IServiceCollection AddLearning(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<LearningLoopOptions>()
            .Bind(configuration.GetSection(LearningLoopOptions.SectionName))
            .Validate(
                options => options.LookbackDays >= 1,
                "Learning loop lookback must be at least one day.")
            .Validate(
                options => options.MinimumMeasuredPosts >= 1,
                "Learning loop minimum measured posts must be at least one.")
            .Validate(
                options => options.MinimumPostsPerGroup >= 1,
                "Learning loop minimum posts per group must be at least one.")
            .Validate(
                options => options.MinimumPostsPerGroup <= options.MinimumMeasuredPosts,
                "Learning loop minimum posts per group cannot exceed the minimum measured posts, or no group could ever be compared.")
            .Validate(
                options => options.MinimumEngagementRateGap >= 0m,
                "Learning loop minimum engagement rate gap cannot be negative.")
            .ValidateOnStart();

        // The metrics reader is registered next to the analytics service it was
        // extracted from, because that is where it belongs: it is one shared
        // read, and registering it twice would let the two features drift apart
        // again.
        services.AddSingleton<LearningInsightPromptBuilder>();
        services.AddScoped<ILearningService, LearningService>();

        return services;
    }
}
