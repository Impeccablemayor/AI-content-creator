using Api.Domain.Enums;

namespace Api.Domain.Entities;

/// <summary>
/// A record of one AI operation: what was asked, which provider and model
/// answered, how long it took, and whether it worked.
/// </summary>
/// <remarks>
/// This is the entity the design calls <c>ai_runs</c>. Its stated purpose is to
/// let the system understand AI usage and cost. It is a diagnostic record, not
/// business data: a caller never reads it to decide what to do, and losing a row
/// must never change the outcome of the operation it describes.
/// <para>
/// Usage and cost are nullable on purpose. The provider abstraction returns
/// only the generated text and does not report token counts, so these columns
/// stay null rather than holding an estimate. An invented cost figure is worse
/// than a missing one, because it would be read back as if it were measured.
/// </para>
/// </remarks>
public sealed class AiRun
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>Which kind of AI operation this was.</summary>
    public AIRunType RunType { get; set; }

    /// <summary>Configured provider name, for example <c>Gemini</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Configured model identifier.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Version of the prompt used, so a later change in output can be traced
    /// to the prompt that produced it.
    /// </summary>
    /// <remarks>
    /// Nullable because the version constants were introduced after the first
    /// runs were recorded; a row written before a prompt was versioned cannot
    /// be given a version that never described it.
    /// </remarks>
    public string? PromptVersion { get; set; }

    /// <summary>Null: the provider abstraction does not report usage.</summary>
    public int? InputTokens { get; set; }

    /// <summary>Null: the provider abstraction does not report usage.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>Null: the provider abstraction does not report usage.</summary>
    public int? TotalTokens { get; set; }

    /// <summary>Null: the provider abstraction does not report usage.</summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// How long the operation took. Null while a run has not completed.
    /// </summary>
    public int? DurationMs { get; set; }

    public AIRunStatus Status { get; set; }

    /// <summary>
    /// Why the run failed, if it did.
    /// </summary>
    /// <remarks>
    /// Holds the exception type name rather than the exception message, matching
    /// how the services already log AI failures. Provider exceptions can quote
    /// the prompt or the raw response, and that content does not belong in a
    /// durable table.
    /// </remarks>
    public string? ErrorMessage { get; set; }

    /// <summary>Structured detail about the run, as a JSON object.</summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the run finished. Null while it is still in progress.</summary>
    public DateTime? CompletedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
