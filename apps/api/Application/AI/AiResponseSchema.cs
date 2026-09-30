namespace Api.Application.AI;

public sealed record AiResponseSchema
{
    public AiResponseSchema(string name, object schema)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "An AI response schema requires a name.",
                nameof(name));
        }

        Name = name;
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public string Name { get; }

    public object Schema { get; }
}
