namespace Api.Application.AI.Models;

/// <summary>
/// What the model returned for one learning signal.
/// </summary>
/// <remarks>
/// A single field on purpose. The observation is not asked for, because the
/// system already produced it from the measured totals and a restatement would
/// be a second chance to misquote a number. The model is only asked for the part
/// that cannot be computed: what the evidence might mean for the next piece of
/// content.
/// </remarks>
public sealed class LearningInsightResult
{
    public string? Recommendation { get; set; }
}
