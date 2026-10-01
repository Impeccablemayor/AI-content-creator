using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.ContentPost;

public sealed class ScheduleContentPostRequest
{
    [Required]
    public DateTimeOffset ScheduledAt { get; set; }
}
