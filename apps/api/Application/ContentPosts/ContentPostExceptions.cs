namespace Api.Application.ContentPosts;

public sealed class ContentIdeaInvalidStatusException : Exception;

public sealed class InvalidContentPostTransitionException : Exception
{
    public InvalidContentPostTransitionException(string message) : base(message)
    {
    }
}

public sealed class InvalidContentPostContentException : Exception
{
    public InvalidContentPostContentException(string message) : base(message)
    {
    }
}

public sealed class InvalidContentPostScheduleTimeException : Exception
{
    public InvalidContentPostScheduleTimeException(string message) : base(message)
    {
    }
}
