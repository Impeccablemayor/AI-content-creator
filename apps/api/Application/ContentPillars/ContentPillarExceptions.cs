namespace Api.Application.ContentPillars;

/// <summary>
/// Thrown when a pillar field is missing or out of range.
/// </summary>
public sealed class InvalidContentPillarRequestException : Exception
{
    public InvalidContentPillarRequestException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Thrown when a workspace already has a pillar with the same name.
/// </summary>
public sealed class DuplicateContentPillarNameException : Exception
{
    public DuplicateContentPillarNameException(string name)
        : base($"A content pillar named '{name}' already exists in this workspace.")
    {
    }
}

/// <summary>
/// Thrown when a referenced audience profile does not belong to the caller's
/// workspace.
/// </summary>
public sealed class ContentPillarAudienceNotFoundException : Exception
{
    public ContentPillarAudienceNotFoundException()
        : base("The target audience profile was not found in this workspace.")
    {
    }
}
