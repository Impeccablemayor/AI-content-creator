namespace Api.Application.ContentEvaluation;

public sealed class ContentEvaluationFailedException : Exception
{
    public ContentEvaluationFailedException(string message) : base(message)
    {
    }
}
