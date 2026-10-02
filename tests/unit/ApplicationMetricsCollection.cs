using Xunit;

namespace Api.Tests;

/// <summary>
/// The classes that assert on the application's instruments share one collection
/// and that collection never runs beside another one.
/// </summary>
/// <remarks>
/// The instruments are process-wide static state. Two classes recording the same
/// series at the same time make a before-and-after assertion depend on the other
/// class, which is how a passing test becomes a flaky one. The instruments have no
/// way to be scoped to a test, so the scheduling has to absorb it instead.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApplicationMetricsCollection
{
    public const string Name = "application-metrics";
}