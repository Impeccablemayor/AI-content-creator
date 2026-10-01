namespace Api.Application.AI;

public static class LearningInsightResponseSchema
{
    public static readonly AiResponseSchema Value = new(
        "learning_insight_recommendation",
        new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "recommendation" },
            properties = new
            {
                recommendation = new { type = "string" }
            }
        });
}
