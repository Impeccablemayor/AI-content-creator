using System.Text.Json;
using Api.Application.Authentication;
using Api.Configuration;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.AI;

/// <summary>
/// Records one row per significant AI operation.
/// </summary>
/// <remarks>
/// Implementations must not throw. A run row describes an operation rather than
/// performing it, so callers record without having to protect the operation
/// they are in the middle of, and without risking a tracking error replacing a
/// real one.
/// </remarks>
public interface IAiRunRecorder
{
    /// <summary>
    /// Records a run that finished successfully.
    /// </summary>
    Task RecordSuccessAsync(
        AIRunType runType,
        string promptVersion,
        TimeSpan duration,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a run that failed.
    /// </summary>
    /// <remarks>
    /// This is called from a catch block that is about to rethrow, so the row
    /// is saved on its own rather than being left for the caller's unit of
    /// work: a failed operation has no business transaction to commit with it.
    /// </remarks>
    Task RecordFailureAsync(
        AIRunType runType,
        string promptVersion,
        TimeSpan duration,
        Exception exception,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IAiRunRecorder"/>
public sealed class AiRunRecorder : IAiRunRecorder
{
    private static readonly JsonSerializerOptions MetadataOptions = new()
    {
        WriteIndented = false
    };

    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger<AiRunRecorder> _logger;

    public AiRunRecorder(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        IOptions<AiOptions> aiOptions,
        ILogger<AiRunRecorder> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _aiOptions = aiOptions;
        _logger = logger;
    }

    public Task RecordSuccessAsync(
        AIRunType runType,
        string promptVersion,
        TimeSpan duration,
        object? metadata = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            runType,
            promptVersion,
            duration,
            AIRunStatus.Succeeded,
            errorMessage: null,
            metadata,
            cancellationToken);

    public Task RecordFailureAsync(
        AIRunType runType,
        string promptVersion,
        TimeSpan duration,
        Exception exception,
        object? metadata = null,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            runType,
            promptVersion,
            duration,
            AIRunStatus.Failed,
            // The type name, not the message: a provider exception can quote the
            // prompt or the raw response back, and that content should not be
            // copied into a durable table. This matches how the services
            // already log AI failures.
            exception.GetType().Name,
            metadata,
            cancellationToken);

    private async Task RecordAsync(
        AIRunType runType,
        string promptVersion,
        TimeSpan duration,
        AIRunStatus status,
        string? errorMessage,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var run = new AiRun
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _currentUser.WorkspaceId,
            RunType = runType,
            Provider = _aiOptions.Value.Provider,
            Model = _aiOptions.Value.Model,
            PromptVersion = promptVersion,
            // InputTokens, OutputTokens, TotalTokens and EstimatedCost are
            // deliberately left null: IAiProvider returns only the generated
            // text, so there is no measured usage to record. They are not
            // estimated from string length, because a guess written to a cost
            // column reads back later as though it had been measured.
            DurationMs = (int)Math.Round(duration.TotalMilliseconds),
            Status = status,
            ErrorMessage = errorMessage,
            Metadata = metadata is null
                ? null
                : JsonSerializer.Serialize(metadata, MetadataOptions),
            CreatedAt = now,
            CompletedAt = now
        };

        try
        {
            _dbContext.AiRuns.Add(run);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // A run row is a diagnostic, so failing to write one must never be
            // allowed to fail the operation it describes. The service is
            // already unwinding, or the work is already done; surfacing a
            // tracking error here would turn a working feature into a broken
            // one, and in the failure path it would replace the real error.
            //
            // The row is detached first. The recorder shares the request's
            // context, so a left-over Added entity would be picked up by the
            // next SaveChanges in the same request and the failed write would
            // resurface as a failure of the operation it was only describing.
            _dbContext.Entry(run).State = EntityState.Detached;
            _logger.LogWarning(
                exception,
                "Failed to record {RunType} AI run for workspace {WorkspaceId}. The operation itself is unaffected.",
                runType,
                run.WorkspaceId);
        }
    }
}
