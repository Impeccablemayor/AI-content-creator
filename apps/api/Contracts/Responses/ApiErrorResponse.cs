namespace Api.Contracts.Responses;

public class ApiErrorResponse
{
    public ApiError Error { get; init; } = new();

    public string TraceId { get; init; } = string.Empty;
}

public class ApiError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public object? Details { get; init; }
}