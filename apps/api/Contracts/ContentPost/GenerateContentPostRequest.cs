using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Api.Domain.Enums;

namespace Api.Contracts.ContentPost;

public sealed class GenerateContentPostRequest
{
    [EnumDataType(typeof(ContentPlatform))]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentPlatform Platform { get; set; }
}