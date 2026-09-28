namespace Api.Contracts.Audience;

public sealed class UpdateAudienceProfileRequest
{
    public string Description { get; set; } = string.Empty;

    public string Demographics { get; set; } = string.Empty;

    public string Interests { get; set; } = string.Empty;

    public string PainPoints { get; set; } = string.Empty;

    public string Goals { get; set; } = string.Empty;
}