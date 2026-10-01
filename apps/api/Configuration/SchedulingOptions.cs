namespace Api.Configuration;

public sealed class SchedulingOptions
{
    public const string SectionName = "Scheduling";

    public int PollIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 25;

    public int ClaimExpiryMinutes { get; set; } = 15;
}
