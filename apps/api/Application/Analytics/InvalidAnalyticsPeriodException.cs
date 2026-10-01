namespace Api.Application.Analytics;

/// <summary>
/// Thrown when a caller supplies a period that can never match, such as a
/// "from" later than its "to".
/// </summary>
public sealed class InvalidAnalyticsPeriodException : Exception
{
    public InvalidAnalyticsPeriodException()
        : base("The analytics period start must not be after its end.")
    {
    }
}
