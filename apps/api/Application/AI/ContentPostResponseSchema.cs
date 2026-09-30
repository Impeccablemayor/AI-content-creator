namespace Api.Application.AI;

public static class ContentPostResponseSchema
{
    public static readonly AiResponseSchema Value = new(
        "content_post_generation",
        new
        {
            type = "object",
            additionalProperties = false,
            required = new[]
            {
                "platform", "content", "hook", "callToAction", "hashtags",
                "contentType", "storytellingStructure"
            },
            properties = new
            {
                platform = new
                {
                    type = "string",
                    @enum = new[] { "LinkedIn", "X" }
                },
                content = new { type = "string" },
                hook = new { type = "string" },
                callToAction = new { type = "string" },
                hashtags = new
                {
                    type = "array",
                    items = new { type = "string" }
                },
                contentType = new
                {
                    type = "string",
                    @enum = new[]
                    {
                        "Educational", "Story", "Opinion", "HowTo", "CaseStudy",
                        "Insight", "Question"
                    }
                },
                storytellingStructure = new { type = "string" }
            }
        });
}
