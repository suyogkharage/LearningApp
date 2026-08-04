using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Common;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace LearningApp.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationAppException ex)
        {
            await HandleAppExceptionAsync(context, ex, ex.Errors);
        }
        catch (AppException ex)
        {
            await HandleAppExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Unhandled exception occurred after the response started for {Method} {Path}", context.Request.Method, context.Request.Path);
                throw;
            }

            logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}, CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier,
                GetCorrelationId(context));

            await WriteAsync(
                context,
                ApiErrorResponse.Create(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.",
                    traceId: context.TraceIdentifier,
                    correlationId: GetCorrelationId(context)));
        }
    }

    private async Task HandleAppExceptionAsync(
        HttpContext context,
        AppException exception,
        IEnumerable<string>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            logger.LogError(exception, "Application exception occurred after the response started for {Method} {Path}", context.Request.Method, context.Request.Path);
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        logger.LogWarning(
            exception,
            "Application exception while processing {Method} {Path}. StatusCode: {StatusCode}, TraceId: {TraceId}, CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            exception.StatusCode,
            context.TraceIdentifier,
            GetCorrelationId(context));

        await WriteAsync(
            context,
            ApiErrorResponse.Create(
                exception.StatusCode,
                exception.Message,
                errors,
                context.TraceIdentifier,
                GetCorrelationId(context)));
    }

    private static async Task WriteAsync(HttpContext context, ApiErrorResponse response)
    {
        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static string? GetCorrelationId(HttpContext context) =>
        context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
}
