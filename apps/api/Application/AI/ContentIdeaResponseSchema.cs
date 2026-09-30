namespace Api.Application.AI;

public static class ContentIdeaResponseSchema
{
    public static readonly AiResponseSchema Value = new(
        "content_idea_generation",
        new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "ideas" },
            properties = new
            {
                ideas = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[]
                        {
                            "title", "topic", "angle", "hook", "reason", "hashtags", "scores"
                        },
                        properties = new
                        {
                            title = new { type = "string" },
                            topic = new { type = "string" },
                            angle = new { type = "string" },
                            hook = new { type = "string" },
                            reason = new { type = "string" },
                            hashtags = new
                            {
                                type = "array",
                                items = new { type = "string" }
                            },
                            scores = new
                            {
                                type = "object",
                                additionalProperties = false,
                                required = new[]
                                {
                                    "relevance", "engagementPotential", "brandFit"
                                },
                                properties = new
                                {
                                    relevance = new { type = "number" },
                                    engagementPotential = new { type = "number" },
                                    brandFit = new { type = "number" }
                                }
                            }
                        }
                    }
                }
            }
        });
}
