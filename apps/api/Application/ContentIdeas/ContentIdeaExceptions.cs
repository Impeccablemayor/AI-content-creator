namespace Api.Application.ContentIdeas;

public sealed class AiProviderUnavailableException : Exception;

public sealed class AiProviderAuthenticationException : Exception;

public sealed class AiProviderRateLimitedException : Exception;

public sealed class AiProviderQuotaExceededException : Exception;

public sealed class AiProviderTimeoutException : Exception;

public sealed class AiProviderRequestException : Exception;

public sealed class InvalidAiResponseException : Exception
{
    public InvalidAiResponseException(string message)
        : base(message)
    {
    }
}

public sealed class ContentIdeaContextIncompleteException : Exception;

public sealed class InvalidContentPostRequestException : Exception
{
    public InvalidContentPostRequestException(string message)
        : base(message)
    {
    }
}

public sealed class InvalidContentIdeaTransitionException : Exception;

public sealed class InvalidContentIdeaRequestException : Exception
{
    public InvalidContentIdeaRequestException(string message)
        : base(message)
    {
    }
}