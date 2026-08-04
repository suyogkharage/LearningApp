namespace LearningApp.UI.Shared.Common;

public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    IReadOnlyCollection<string> Errors)
{
    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.") =>
        new(true, message, data, Array.Empty<string>());

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) =>
        new(false, message, default, errors?.ToArray() ?? Array.Empty<string>());
}

public sealed record ApiErrorResponse(
    bool Success,
    int StatusCode,
    string Message,
    IReadOnlyCollection<string>? Errors,
    string? TraceId,
    string? CorrelationId,
    DateTimeOffset Timestamp)
{
    public static ApiErrorResponse Create(
        int statusCode,
        string message,
        IEnumerable<string>? errors = null,
        string? traceId = null,
        string? correlationId = null) =>
        new(
            false,
            statusCode,
            message,
            errors?.ToArray(),
            traceId,
            correlationId,
            DateTimeOffset.UtcNow);
}
