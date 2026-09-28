using Api.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.ContentIdea;

public sealed class GenerateContentIdeasRequest
{
    [EnumDataType(typeof(ContentPlatform))]
    public ContentPlatform Platform { get; set; }

    [Range(1, 20)]
    public int Count { get; set; } = 5;

    [StringLength(300)]
    public string? TopicHint { get; set; }
}