using Api.Application.AI;
using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Domain.Enums;

namespace Api.IntegrationTests;

/// <summary>
/// Stands in for the two edges that cannot exist in a test run: the social platform
/// and the AI model.
/// </summary>
/// <remarks>
/// <para>
/// Everything between those edges is the real implementation. The scheduler, the
/// publish service, the credential protector, the metrics store, the reporting
/// service, the analyser, and the persistence are the production types resolved from
/// the production container, against a real PostgreSQL. Substituting a network
/// dependency is what makes the test possible; substituting the database, which is
/// the part being tested, would make it worthless.
/// </para>
/// <para>
/// The publisher reports the metrics a post will later return by carrying them in the
/// external post id, which the metrics provider then reads back. That keeps the two
/// stubs independent and the run order irrelevant, where a shared mutable table of
/// expectations would make a second test in the same class depend on the first.
/// </para>
/// </remarks>
public static class StubPlatform
{
    private const string MarkerPrefix = "mayorx-metrics:";

    public static string MetricsMarker(long impressions, long likes, long comments) =>
        $"{MarkerPrefix}{impressions}:{likes}:{comments}";

    internal static SocialMetricsSnapshot SnapshotFrom(string externalPostId)
    {
        var value = externalPostId.StartsWith(MarkerPrefix, StringComparison.Ordinal)
            ? externalPostId[MarkerPrefix.Length..]
            : throw new InvalidOperationException(
                $"The stub platform was asked about unknown post '{externalPostId}'.");

        var parts = value.Split(':');

        return new SocialMetricsSnapshot(
            Impressions: long.Parse(parts[0]),
            Likes: long.Parse(parts[1]),
            Comments: long.Parse(parts[2]));
    }

    internal static string? FindMarker(string text)
    {
        var index = text.IndexOf(MarkerPrefix, StringComparison.Ordinal);

        return index < 0 ? null : text[index..].Split('\n')[0].Trim();
    }
}

public sealed class StubLinkedInPublisher : ISocialPlatformPublisher
{
    public ContentPlatform Platform => ContentPlatform.LinkedIn;

    public bool IsConfigured => true;

    public Task<SocialPublishResult> PublishAsync(
        string accessToken,
        SocialPublishRequest request,
        CancellationToken cancellationToken)
    {
        var marker = StubPlatform.FindMarker(request.Text)
            ?? throw new InvalidOperationException(
                "The seeded content did not carry a metrics marker, so the stub " +
                "platform cannot tell which metrics this post should report.");

        return Task.FromResult(new SocialPublishResult(
            marker,
            $"https://www.linkedin.com/feed/update/{marker}"));
    }
}

public sealed class StubLinkedInMetricsProvider : ISocialPlatformMetricsProvider
{
    public ContentPlatform Platform => ContentPlatform.LinkedIn;

    public bool IsConfigured => true;

    public Task<SocialMetricsSnapshot> FetchAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken) =>
        Task.FromResult(StubPlatform.SnapshotFrom(externalPostId));
}

/// <summary>
/// Returns the one shape the learning prompt accepts.
/// </summary>
/// <remarks>
/// The analysis, the grouping, the margin rule, and the persistence are all real;
/// only the sentence of advice is a fixed string, so the assertion is about the
/// measurement and not about what a model happened to write.
/// </remarks>
public sealed class StubAiProvider : IAiProvider
{
    public const string Recommendation = "Double down on the pillar that outperformed.";

    public Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        AiResponseSchema responseSchema,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            $$"""{"recommendation": "{{Recommendation}}"}""");
}
