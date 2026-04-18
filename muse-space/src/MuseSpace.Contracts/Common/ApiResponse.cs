namespace MuseSpace.Contracts.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RequestId { get; init; }

    public static ApiResponse<T> Ok(T data, string? requestId = null) => new()
    {
        Success = true,
        Data = data,
        RequestId = requestId
    };

    public static ApiResponse<T> Fail(string errorMessage, string? requestId = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        RequestId = requestId
    };
}
