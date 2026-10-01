namespace Api.Domain.Enums;

/// <summary>
/// Whether an AI run finished successfully or failed.
/// </summary>
/// <remarks>
/// The schema names this type <c>AIRunStatus</c> but does not list its members,
/// so the set is kept to the two outcomes a recorded run can actually have. A
/// run is written once it has finished, so there is no "in progress" state to
/// represent; adding one later would mean a new member, not a migration.
/// </remarks>
public enum AIRunStatus
{
    Succeeded = 1,

    Failed = 2
}

/// <summary>
/// Converts <see cref="AIRunStatus"/> to and from the stored strings.
/// </summary>
/// <remarks>
/// The schema uses SCREAMING_SNAKE_CASE for its enumerated string columns, as
/// it does for job and publication statuses.
/// </remarks>
public static class AIRunStatusStorage
{
    private static readonly Dictionary<AIRunStatus, string> Values = new()
    {
        [AIRunStatus.Succeeded] = "SUCCEEDED",
        [AIRunStatus.Failed] = "FAILED"
    };

    public static string ToStorage(AIRunStatus value) => Values[value];

    public static AIRunStatus FromStorage(string value) =>
        Values.FirstOrDefault(pair =>
            string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase)).Key;
}
