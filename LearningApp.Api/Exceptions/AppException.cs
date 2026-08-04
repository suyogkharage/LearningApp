namespace LearningApp.Api.Exceptions;

public class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string message)
    : AppException(message, StatusCodes.Status404NotFound);

public sealed class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict);

public sealed class ValidationAppException(IReadOnlyCollection<string> errors)
    : AppException("Validation failed.", StatusCodes.Status400BadRequest)
{
    public IReadOnlyCollection<string> Errors { get; } = errors;
}
