using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// How the API behaves when the configuration it depends on is wrong.
/// </summary>
/// <remarks>
/// The choice being tested is that a bad security setting stops the process
/// rather than starting with a silently weakened policy. A deployment that
/// starts with an unintended CORS policy or a nonsensical throttle is worse than
/// one that does not start, because it is discovered by someone else.
/// </remarks>
public sealed class SecurityConfigurationTests
{
    [Theory]
    [InlineData("-1")]
    [InlineData("-100")]
    public void A_negative_permit_limit_stops_startup(string permitLimit)
    {
        var failure = AssertStartupFails(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthenticationPermitLimit"] = permitLimit
        });

        Assert.Contains("permit limit", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    public void A_non_positive_window_stops_startup(string window)
    {
        var failure = AssertStartupFails(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthenticationWindow"] = window
        });

        Assert.Contains("window", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_non_numeric_window_stops_startup()
    {
        var failure = AssertStartupFails(new Dictionary<string, string?>
        {
            ["RateLimiting:AuthenticationWindow"] = "about a minute"
        });

        Assert.Contains(
            "AuthenticationWindow",
            failure.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_zero_permit_limit_is_allowed_and_means_no_throttling()
    {
        // Turning throttling off is a legitimate operational choice, so it must
        // not be mistaken for a misconfiguration.
        using var factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = "0"
            });

        factory.Initialise();

        Assert.NotNull(factory.CreateClient());
    }

    private static Exception AssertStartupFails(
        IReadOnlyDictionary<string, string?> settings)
    {
        using var factory = MayorxApiFactory.WithSettings(settings);

        factory.Initialise();

        try
        {
            factory.CreateClient()
                .GetAsync("api/v1/health/live")
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception)
        {
            return exception;
        }

        Assert.Fail(
            "The host started despite an invalid security configuration.");

        throw new InvalidOperationException();
    }
}