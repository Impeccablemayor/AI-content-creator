using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Api.Domain.Enums;

namespace Api.Contracts.ContentPost;

public sealed class UpdateContentPostRequest
{
    [Required]
    [StringLength(3000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Hook { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string CallToAction { get; set; } = string.Empty;

    [Required]
    public List<string>? Hashtags { get; set; }

    [EnumDataType(typeof(ContentType))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentType ContentType { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string StorytellingStructure { get; set; } = string.Empty;
}
