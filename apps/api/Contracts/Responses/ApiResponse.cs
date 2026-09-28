namespace Api.Contracts.Responses;

public class ApiResponse<T>
{
    public T Data { get; init; } = default!;

    public string Message { get; init; } = string.Empty;
}