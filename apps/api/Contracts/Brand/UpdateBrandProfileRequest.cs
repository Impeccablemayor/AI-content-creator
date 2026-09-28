namespace Api.Contracts.Brand;

public sealed class UpdateBrandProfileRequest
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Industry { get; set; } = string.Empty;

    public string ToneOfVoice { get; set; } = string.Empty;

    public string ContentGoals { get; set; } = string.Empty;
}