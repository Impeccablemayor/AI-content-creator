using System.Net;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The one place a social platform failure is classified is also the one place it
/// is counted, which is what makes the count exact: a new provider path cannot be
/// added without being counted.
/// </summary>
[Collection(ApplicationMetricsCollection.Name)]
public sealed class SocialProviderHttpFailuresTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "SocialProviderAuthenticationException")]
    [InlineData(HttpStatusCode.Forbidden, "SocialProviderAuthenticationException")]
    [InlineData(HttpStatusCode.RequestTimeout, "SocialProviderUnavailableException")]
    [InlineData(HttpStatusCode.TooManyRequests, "SocialProviderUnavailableException")]
    [InlineData(HttpStatusCode.InternalServerError, "SocialProviderUnavailableException")]
    [InlineData(HttpStatusCode.BadGateway, "SocialProviderUnavailableException")]
    [InlineData(HttpStatusCode.BadRequest, "SocialProviderRequestException")]
    [InlineData(HttpStatusCode.NotFound, "SocialProviderRequestException")]
    public void A_status_code_becomes_the_exception_the_application_reasons_about(
        HttpStatusCode statusCode,
        string expectedException)
    {
        var exception = SocialProviderHttpFailures.FromStatusCode(
            statusCode,
            SocialProviderOperation.Publish,
            ContentPlatform.LinkedIn);

        Assert.Equal(expectedException, exception.GetType().Name);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SocialProviderFailureClass.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, SocialProviderFailureClass.Authentication)]
    [InlineData(HttpStatusCode.RequestTimeout, SocialProviderFailureClass.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, SocialProviderFailureClass.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SocialProviderFailureClass.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, SocialProviderFailureClass.Request)]
    public void A_status_code_belongs_to_one_failure_class(
        HttpStatusCode statusCode,
        SocialProviderFailureClass expected) =>
        Assert.Equal(expected, SocialProviderHttpFailures.Classify(statusCode));

    [Fact]
    public void A_rejected_credential_is_counted_as_an_authentication_failure()
    {
        // The distinction an operator acts on: a revoked token needs reconnecting,
        // a throttled one needs waiting.
        var before = ProviderFailureCount(ContentPlatform.LinkedIn, "publish", "authentication");

        SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.Unauthorized,
            SocialProviderOperation.Publish,
            ContentPlatform.LinkedIn);

        Assert.Equal(
            before + 1,
            ProviderFailureCount(ContentPlatform.LinkedIn, "publish", "authentication"));
    }

    [Fact]
    public void A_throttled_provider_is_counted_as_unavailable()
    {
        var before = ProviderFailureCount(ContentPlatform.X, "publish", "unavailable");

        SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.TooManyRequests,
            SocialProviderOperation.Publish,
            ContentPlatform.X);

        Assert.Equal(
            before + 1,
            ProviderFailureCount(ContentPlatform.X, "publish", "unavailable"));
    }

    [Fact]
    public void A_server_error_is_counted_as_unavailable()
    {
        var before = ProviderFailureCount(ContentPlatform.LinkedIn, "metrics", "unavailable");

        SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.InternalServerError,
            SocialProviderOperation.Metrics,
            ContentPlatform.LinkedIn);

        Assert.Equal(
            before + 1,
            ProviderFailureCount(ContentPlatform.LinkedIn, "metrics", "unavailable"));
    }

    [Fact]
    public void An_unreadable_response_is_counted_as_a_request_failure()
    {
        var before = ProviderFailureCount(ContentPlatform.X, "oauth_token", "request");

        SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.BadRequest,
            SocialProviderOperation.OAuthToken,
            ContentPlatform.X);

        Assert.Equal(
            before + 1,
            ProviderFailureCount(ContentPlatform.X, "oauth_token", "request"));
    }

    [Fact]
    public void The_two_operations_that_share_an_endpoint_are_counted_separately()
    {
        // A failure while reading the profile and a failure while exchanging the
        // token are different incidents even though both go to the platform.
        var profile = ProviderFailureCount(ContentPlatform.LinkedIn, "oauth_profile", "authentication");
        var token = ProviderFailureCount(ContentPlatform.LinkedIn, "oauth_token", "authentication");

        SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.Unauthorized,
            SocialProviderOperation.OAuthProfile,
            ContentPlatform.LinkedIn);

        Assert.Equal(profile + 1, ProviderFailureCount(ContentPlatform.LinkedIn, "oauth_profile", "authentication"));
        Assert.Equal(token, ProviderFailureCount(ContentPlatform.LinkedIn, "oauth_token", "authentication"));
    }

    [Fact]
    public void A_failure_is_counted_once_and_the_exception_is_returned_for_the_caller_to_throw()
    {
        var before = ProviderFailureCount(ContentPlatform.X, "oauth_token", "authentication");

        var exception = SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.Forbidden,
            SocialProviderOperation.OAuthToken,
            ContentPlatform.X);

        Assert.IsType<SocialProviderAuthenticationException>(exception);
        Assert.Equal(
            before + 1,
            ProviderFailureCount(ContentPlatform.X, "oauth_token", "authentication"));
    }

    [Fact]
    public void A_failure_metric_never_carries_the_status_code_or_a_response_body()
    {
        // The status is folded into the failure class, so the exact code does not
        // become a label, and no body was available to record in the first place.
        // The exception may still quote the code: that is a log line, not a series.
        var before = ProviderFailureCount(ContentPlatform.LinkedIn, "publish", "request");

        SocialProviderHttpFailures.FromStatusCode(
            HttpStatusCode.NotFound,
            SocialProviderOperation.Publish,
            ContentPlatform.LinkedIn);

        Assert.Equal(before + 1, ProviderFailureCount(ContentPlatform.LinkedIn, "publish", "request"));

        var series = MetricsScrape.Series("mayorx_external_provider_failures_total");

        Assert.All(series, sample => Assert.Equal(
            ["failure_class", "operation", "platform"],
            sample.Labels.Select(label => label.Key).OrderBy(key => key, StringComparer.Ordinal)));
        Assert.All(series, sample => Assert.All(
            sample.Labels,
            label => Assert.DoesNotContain("404", label.Value, StringComparison.Ordinal)));
    }

    private static long ProviderFailureCount(
        ContentPlatform platform,
        string operation,
        string failureClass) =>
        MetricsScrape.CounterOrZero(
            "mayorx_external_provider_failures_total",
            ("platform", platform == ContentPlatform.LinkedIn ? "linkedin" : "x"),
            ("operation", operation),
            ("failure_class", failureClass));
}