using Api.Application.Authentication;
using Api.Contracts.Analytics;
using Api.Domain.Enums;

namespace Api.Application.Analytics;

/// <summary>
/// Optional filters shared by the analytics reads. A null bound is unbounded,
/// not defaulted, so the response can report the period that was really used.
/// </summary>
public sealed record AnalyticsQuery(
    ContentPlatform? Platform = null,
    DateTime? From = null,
    DateTime? To = null,
    Guid? PillarId = null);

public interface IAnalyticsService
{
    Task<AnalyticsOverviewResponse> GetOverviewAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostAnalyticsResponse>> GetPostsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    Task<PostAnalyticsResponse?> GetPostAsync(
        Guid contentPostId,
        CancellationToken cancellationToken = default);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly CurrentUserService _currentUser;
    private readonly IPublishedPostMetricsReader _reader;

    public AnalyticsService(CurrentUserService currentUser, IPublishedPostMetricsReader reader)
    {
        _currentUser = currentUser;
        _reader = reader;
    }

    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var measured = await _reader.ReadAsync(
            _currentUser.WorkspaceId,
            query,
            cancellationToken);

        var latest = measured.Select(item => item.Latest).ToList();

        var totals = new MetricTotals(
            measured.Count,
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.Impressions)),
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.Likes)),
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.Comments)),
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.Shares)),
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.Clicks)),
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.ProfileVisits)),
            AnalyticsCalculator.Sum(latest.Select(metric => metric?.FollowersGained)));

        return new AnalyticsOverviewResponse
        {
            Period = new AnalyticsPeriodResponse
            {
                From = query.From,
                To = query.To
            },
            PostsPublished = totals.PostsPublished,
            Impressions = totals.Impressions,
            Likes = totals.Likes,
            Comments = totals.Comments,
            Shares = totals.Shares,
            Clicks = totals.Clicks,
            ProfileVisits = totals.ProfileVisits,
            FollowersGained = totals.FollowersGained,
            EngagementRate = AnalyticsCalculator.EngagementRate(
                totals.Likes,
                totals.Comments,
                totals.Shares,
                totals.Impressions)
        };
    }

    public async Task<IReadOnlyList<PostAnalyticsResponse>> GetPostsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var measured = await _reader.ReadAsync(
            _currentUser.WorkspaceId,
            query,
            cancellationToken);

        return measured.Select(item => Map(item)).ToList();
    }

    public async Task<PostAnalyticsResponse?> GetPostAsync(
        Guid contentPostId,
        CancellationToken cancellationToken = default)
    {
        var measured = await _reader.ReadOneAsync(
            _currentUser.WorkspaceId,
            contentPostId,
            cancellationToken);

        return measured is null ? null : Map(measured);
    }

    private static PostAnalyticsResponse Map(MeasuredPost measured)
    {
        var post = measured.Post;
        var latest = measured.Latest;

        return new PostAnalyticsResponse
        {
            ContentPostId = post.Id,
            Platform = post.Platform.ToString(),
            ContentPillarId = post.ContentIdea?.ContentPillar?.Id ?? post.ContentIdea?.ContentPillarId,
            ContentPillarName = post.ContentIdea?.ContentPillar?.Name,
            PublishedAt = post.PublishedAt,
            RecordedAt = latest?.RecordedAt,
            Impressions = latest?.Impressions,
            Likes = latest?.Likes,
            Comments = latest?.Comments,
            Shares = latest?.Shares,
            Clicks = latest?.Clicks,
            ProfileVisits = latest?.ProfileVisits,
            FollowersGained = latest?.FollowersGained,
            EngagementRate = AnalyticsCalculator.EngagementRate(
                latest?.Likes,
                latest?.Comments,
                latest?.Shares,
                latest?.Impressions)
        };
    }

    private sealed record MetricTotals(
        int PostsPublished,
        long? Impressions,
        long? Likes,
        long? Comments,
        long? Shares,
        long? Clicks,
        long? ProfileVisits,
        long? FollowersGained);
}
