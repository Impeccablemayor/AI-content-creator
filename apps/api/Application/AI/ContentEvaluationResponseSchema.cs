namespace Api.Application.AI;

public static class ContentEvaluationResponseSchema
{
    private static readonly string[] DimensionNames =
    [
        "strategicFit",
        "audienceFit",
        "hookStrength",
        "value",
        "clarity",
        "originality",
        "storytelling",
        "platformFit",
        "voiceMatch",
        "factualReliability"
    ];

    private static readonly string[] RequiredFields =
        [.. DimensionNames, "strengths", "issues"];

    public static readonly AiResponseSchema Value = new(
        "content_quality_evaluation",
        new
        {
            type = "object",
            additionalProperties = false,
            required = RequiredFields,
            properties = new
            {
                strategicFit = Score(),
                audienceFit = Score(),
                hookStrength = Score(),
                value = Score(),
                clarity = Score(),
                originality = Score(),
                storytelling = Score(),
                platformFit = Score(),
                voiceMatch = Score(),
                factualReliability = Score(),
                strengths = StringList(),
                issues = StringList()
            }
        });

    private static object Score() => new
    {
        type = "number",
        minimum = 0,
        maximum = 100
    };

    private static object StringList() => new
    {
        type = "array",
        maxItems = 5,
        items = new { type = "string" }
    };
}
