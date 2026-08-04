# Global Error Response Structure in LearningApp.Api

This document now reflects the current implementation in this project.

`LearningApp.Api` uses two response models:

| Response type | Used for | File |
|---|---|---|
| `ApiResponse<T>` | Successful API responses | `LearningApp.UI/LearningApp.UI.Shared/Common/ApiResponse.cs` |
| `ApiErrorResponse` | Error API responses | `LearningApp.UI/LearningApp.UI.Shared/Common/ApiResponse.cs` |

Success responses keep the existing shape:

```json
{
  "success": true,
  "message": "Category created.",
  "data": {
    "categoryId": "..."
  },
  "errors": []
}
```

Error responses use this shape:

```json
{
  "success": false,
  "statusCode": 404,
  "message": "Category was not found.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000001",
  "correlationId": "9c2ecf8f-8f62-41e2-8f61-99d21cb9af15",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

`traceId` comes from `HttpContext.TraceIdentifier`.

`correlationId` comes from the optional request header:

```http
X-Correlation-Id: 9c2ecf8f-8f62-41e2-8f61-99d21cb9af15
```

If the request does not send `X-Correlation-Id`, the response returns `correlationId: null`.

---

## Current Project Implementation

### 1. Error Response Model

Current model:

```csharp
public sealed record ApiErrorResponse(
    bool Success,
    int StatusCode,
    string Message,
    IReadOnlyCollection<string>? Errors,
    string? TraceId,
    string? CorrelationId,
    DateTimeOffset Timestamp)
```

The model also has a factory method:

```csharp
public static ApiErrorResponse Create(
    int statusCode,
    string message,
    IEnumerable<string>? errors = null,
    string? traceId = null,
    string? correlationId = null)
```

This keeps response creation consistent across middleware, validation, authentication, and controller-specific error responses.

### 2. Custom Application Exceptions

File:

```text
LearningApp.Api/Exceptions/AppException.cs
```

Current exception hierarchy:

```csharp
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
```

Current mapping:

| Exception | HTTP status | Example |
|---|---:|---|
| `ValidationAppException` | 400 | Business/service validation failed |
| `NotFoundException` | 404 | Category, course, user, enrollment, or progress item was not found |
| `ConflictException` | 409 | Duplicate category/user/enrollment conflict |
| Any other `Exception` | 500 | Unexpected application/server failure |

### 3. Global Exception Middleware

File:

```text
LearningApp.Api/Middleware/ExceptionHandlingMiddleware.cs
```

Current catch order:

```csharp
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
    // Log unexpected failure and return safe 500 response.
}
```

`ValidationAppException` has a separate catch block because it carries detailed validation errors in `ex.Errors`.

`ConflictException` and `NotFoundException` are caught by the `catch (AppException ex)` block because both inherit from `AppException`.

Known application exceptions are logged with `LogWarning` because they are controlled business/client errors. Unexpected exceptions are logged with `LogError` because they may indicate bugs or infrastructure failures.

Unexpected exceptions do not expose raw exception messages to the client. The client receives:

```json
{
  "success": false,
  "statusCode": 500,
  "message": "An unexpected error occurred.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000002",
  "correlationId": null,
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

### 4. Middleware Registration

File:

```text
LearningApp.Api/Program.cs
```

Current registration:

```csharp
app.UseLearningAppExceptionHandling();
app.UseSerilogRequestLogging();
```

The extension method is in:

```text
LearningApp.Api/Extensions/ServiceCollectionExtensions.cs
```

```csharp
public static IApplicationBuilder UseLearningAppExceptionHandling(this IApplicationBuilder app) =>
    app.UseMiddleware<ExceptionHandlingMiddleware>();
```

The exception middleware is registered early so it can catch exceptions from later middleware and controller actions.

### 5. Automatic Model Validation Errors

File:

```text
LearningApp.Api/Extensions/ServiceCollectionExtensions.cs
```

`ApiBehaviorOptions.InvalidModelStateResponseFactory` is configured so automatic `[ApiController]` model validation errors also return `ApiErrorResponse`.

Example response:

```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    "The Name field is required."
  ],
  "traceId": "0HN7L9KJ4FQ2M:00000003",
  "correlationId": null,
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

This is separate from `ValidationAppException`.

Use automatic model validation for DTO binding/attribute validation. Use `ValidationAppException` for business/service validation.

### 6. JWT Authentication And Authorization Errors

File:

```text
LearningApp.Api/Extensions/ServiceCollectionExtensions.cs
```

The API customizes JWT bearer events:

| Event | Status | Message |
|---|---:|---|
| `OnChallenge` | 401 | `You are not authenticated.` |
| `OnForbidden` | 403 | `You do not have permission to access this resource.` |

Missing, invalid, or expired token:

```json
{
  "success": false,
  "statusCode": 401,
  "message": "You are not authenticated.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000004",
  "correlationId": null,
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

Authenticated user without permission:

```json
{
  "success": false,
  "statusCode": 403,
  "message": "You do not have permission to access this resource.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000005",
  "correlationId": null,
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

### 7. Disabled Local JWT Login Endpoint

File:

```text
LearningApp.Api/Controllers/AuthController.cs
```

The old local API-issued JWT login endpoint is disabled. It returns `410 Gone` using `ApiErrorResponse`.

Example:

```json
{
  "success": false,
  "statusCode": 410,
  "message": "Local API-issued JWT login is disabled. Use Microsoft account sign-in through the Blazor UI.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000006",
  "correlationId": null,
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

### 8. How To Add New Error Scenarios

Use this rule when adding or changing API code:

| Scenario | Current project pattern |
|---|---|
| Success | Return `ApiResponse<T>` |
| Resource not found | Throw `NotFoundException` |
| Duplicate/conflict | Throw `ConflictException` |
| Business validation failure | Throw `ValidationAppException` |
| Unexpected technical failure | Let it bubble to middleware |
| Request DTO/model validation | Let `[ApiController]` and `ApiBehaviorOptions` handle it |
| Authentication/authorization failure | Let JWT/auth middleware handle it |

Avoid repeated controller-level `try/catch` blocks just to format errors. Controllers should focus on request and success response flow. The middleware and framework hooks handle error formatting.

---

## Reference Notes

The rest of this document keeps the original learning notes for background and interview preparation. The sections above are the source of truth for the current `LearningApp.Api` implementation.

---

# Original Learning Notes: Global Error Response Structure in ASP.NET Core Web API

## 1. What Problem Are We Solving?

In real-world Web API projects, different APIs should not return errors in different formats.

Bad examples:

```json
"User not found"
```

```json
{
  "error": "Invalid order id"
}
```

```json
{
  "message": "Something went wrong",
  "status": 500
}
```

This creates problems for frontend applications because every error response must be handled differently.

A better approach is to return a consistent error response structure from all APIs.

---

## 2. What Is a Global Error Response Structure?

A global error response structure means:

> Every API error should return the same response shape.

Example:

```json
{
  "success": false,
  "statusCode": 404,
  "message": "User not found.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000001",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

Validation error example:

```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    "Name is required.",
    "Email format is invalid."
  ],
  "traceId": "0HN7L9KJ4FQ2M:00000002",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

---

## 3. Why Is This Useful?

A global error response structure helps with:

1. Consistent frontend error handling
2. Cleaner API design
3. Better debugging
4. Better production support
5. Avoiding stack trace exposure
6. Better logging with `traceId` or `correlationId`
7. Easier integration with Blazor, Angular, React, mobile apps, and external systems

Frontend handling becomes simpler:

```javascript
if (!response.ok) {
    const error = await response.json();
    alert(error.message);
}
```

---

## 4. Recommended Error Response Model

```csharp
public class ApiErrorResponse
{
    public bool Success { get; set; } = false;

    public int StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public List<string>? Errors { get; set; }

    public string? TraceId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

Example response:

```json
{
  "success": false,
  "statusCode": 500,
  "message": "An unexpected error occurred.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000003",
  "correlationId": "9c2ecf8f-8f62-41e2-8f61-99d21cb9af15",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

---

## 5. Success Response vs Error Response

For successful responses, you may use a generic response wrapper:

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public List<string>? Errors { get; set; }
}
```

Example success response:

```json
{
  "success": true,
  "message": "User fetched successfully.",
  "data": {
    "id": 1,
    "name": "Suyog"
  },
  "errors": null
}
```

In real-world projects, it is often cleaner to use:

```text
ApiResponse<T>      -> success responses
ApiErrorResponse    -> error responses
```

Error responses usually need extra fields such as `statusCode`, `traceId`, `correlationId`, and `timestamp`.

---

## 6. Where Should Errors Be Handled?

| Error Type | Recommended Place |
|---|---|
| Unhandled exceptions | Global exception middleware |
| Validation errors | `ApiBehaviorOptions` or action filter |
| Business errors | Service/controller using custom exceptions |
| Unauthorized | Authentication middleware |
| Forbidden | Authorization middleware |
| Not found | Service/controller level |
| Bad request | Controller/model validation |

---

## 7. Global Exception Middleware

Create a middleware class:

```text
Middlewares/ExceptionHandlingMiddleware.cs
```

```csharp
using System.Text.Json;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var message = "An unexpected error occurred.";
        List<string>? errors = null;

        if (exception is AppException appException)
        {
            statusCode = appException.StatusCode;
            message = appException.Message;
        }

        var correlationId = context.Request.Headers["X-Correlation-Id"]
            .FirstOrDefault();

        _logger.LogError(
            exception,
            "Exception occurred. StatusCode: {StatusCode}, TraceId: {TraceId}, CorrelationId: {CorrelationId}",
            statusCode,
            context.TraceIdentifier,
            correlationId);

        var response = new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            Errors = errors,
            TraceId = context.TraceIdentifier,
            CorrelationId = correlationId
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await context.Response.WriteAsync(json);
    }
}
```

Register middleware in `Program.cs`:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
```

Place exception middleware early in the pipeline so it can catch exceptions from later middleware and controller actions.

---

## 8. Custom Exception Classes

Not every exception should return `500 Internal Server Error`.

Examples:

```text
User not found          -> 404
Validation failed       -> 400
Duplicate email         -> 409
Unauthorized operation  -> 403
```

### Base AppException

```csharp
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
```

### NotFoundException

```csharp
public class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message, StatusCodes.Status404NotFound)
    {
    }
}
```

### BadRequestException

```csharp
public class BadRequestException : AppException
{
    public BadRequestException(string message)
        : base(message, StatusCodes.Status400BadRequest)
    {
    }
}
```

### ConflictException

```csharp
public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, StatusCodes.Status409Conflict)
    {
    }
}
```

### ForbiddenException

```csharp
public class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base(message, StatusCodes.Status403Forbidden)
    {
    }
}
```

---

## 9. Practical Service Usage

Instead of returning `null` and checking everywhere, the service can throw a controlled exception.

```csharp
public async Task<UserDto> GetUserByIdAsync(int id)
{
    var user = await _dbContext.Users.FindAsync(id);

    if (user == null)
    {
        throw new NotFoundException($"User with id {id} was not found.");
    }

    return new UserDto
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email
    };
}
```

Controller stays clean:

```csharp
[Authorize]
[HttpGet("{id}")]
public async Task<IActionResult> GetUserById(int id)
{
    var user = await _userService.GetUserByIdAsync(id);

    return Ok(user);
}
```

If the user is not found, middleware automatically returns:

```json
{
  "success": false,
  "statusCode": 404,
  "message": "User with id 10 was not found.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000004",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

---

## 10. Validation Error Response

When `[ApiController]` is used, ASP.NET Core automatically returns `400 Bad Request` for invalid model state.

To customize validation response format, configure `ApiBehaviorOptions`.

```csharp
using Microsoft.AspNetCore.Mvc;

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        var response = new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Validation failed.",
            Errors = errors,
            TraceId = context.HttpContext.TraceIdentifier
        };

        return new BadRequestObjectResult(response);
    };
});
```

DTO example:

```csharp
public class CreateUserRequest
{
    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email format is invalid.")]
    public string Email { get; set; } = string.Empty;
}
```

Invalid request:

```json
{
  "name": "",
  "email": "wrong-email"
}
```

Response:

```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    "Name is required.",
    "Email format is invalid."
  ],
  "traceId": "0HN7L9KJ4FQ2M:00000005",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

---

## 11. Handling 401 and 403 Responses

With JWT authentication, default `401` and `403` responses may not follow your custom structure.

You can customize JWT bearer events.

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Token validation settings
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new ApiErrorResponse
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    Message = "You are not authenticated.",
                    TraceId = context.HttpContext.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            },

            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var response = new ApiErrorResponse
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    Message = "You do not have permission to access this resource.",
                    TraceId = context.HttpContext.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        };
    });
```

### Missing, invalid, or expired token

```json
{
  "success": false,
  "statusCode": 401,
  "message": "You are not authenticated.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000006",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

### Valid token but no permission

```json
{
  "success": false,
  "statusCode": 403,
  "message": "You do not have permission to access this resource.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000007",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

---

## 12. Should We Expose Exception Messages?

For known business exceptions, yes.

Examples:

```text
User not found.
Email already exists.
Invalid order status.
```

For unknown exceptions, no.

Bad production response:

```json
{
  "message": "Object reference not set to an instance of an object at OrderService.cs line 52"
}
```

Good production response:

```json
{
  "message": "An unexpected error occurred.",
  "traceId": "0HN7L9KJ4FQ2M:00000008"
}
```

Detailed technical exception information should go into logs, not API responses.

---

## 13. Error Response with Correlation ID

In enterprise systems, a `correlationId` helps trace a request across multiple systems.

Request header:

```http
X-Correlation-Id: 9c2ecf8f-8f62-41e2-8f61-99d21cb9af15
```

Error response:

```json
{
  "success": false,
  "statusCode": 500,
  "message": "An unexpected error occurred.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000008",
  "correlationId": "9c2ecf8f-8f62-41e2-8f61-99d21cb9af15",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

This is useful when checking logs in tools like Seq, Application Insights, or ELK.

---

## 14. Practical Folder Structure

For a clean architecture-style project:

```text
src
 ├── Application
 │    ├── Exceptions
 │    │    ├── AppException.cs
 │    │    ├── NotFoundException.cs
 │    │    ├── BadRequestException.cs
 │    │    └── ConflictException.cs
 │    └── Common
 │         └── Models
 │              └── ApiErrorResponse.cs
 │
 ├── Infrastructure
 │
 └── WebApi
      ├── Middlewares
      │    └── ExceptionHandlingMiddleware.cs
      ├── Extensions
      │    └── MiddlewareExtensions.cs
      └── Program.cs
```

For a simpler project:

```text
Models
 └── ApiErrorResponse.cs

Exceptions
 ├── AppException.cs
 ├── NotFoundException.cs
 └── ConflictException.cs

Middlewares
 └── ExceptionHandlingMiddleware.cs
```

---

## 15. Middleware Extension Method

Instead of writing this directly:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

Create an extension method:

```csharp
public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
```

Then in `Program.cs`:

```csharp
app.UseGlobalExceptionHandling();
```

This keeps `Program.cs` cleaner.

---

## 16. Practical Example: Duplicate Email

Service:

```csharp
public async Task CreateUserAsync(CreateUserRequest request)
{
    var emailExists = await _dbContext.Users
        .AnyAsync(x => x.Email == request.Email);

    if (emailExists)
    {
        throw new ConflictException("Email already exists.");
    }

    var user = new User
    {
        Name = request.Name,
        Email = request.Email
    };

    _dbContext.Users.Add(user);
    await _dbContext.SaveChangesAsync();
}
```

Controller:

```csharp
[HttpPost]
public async Task<IActionResult> CreateUser(CreateUserRequest request)
{
    await _userService.CreateUserAsync(request);

    return Created();
}
```

Response:

```json
{
  "success": false,
  "statusCode": 409,
  "message": "Email already exists.",
  "errors": null,
  "traceId": "0HN7L9KJ4FQ2M:00000009",
  "timestamp": "2026-07-06T14:32:10.123Z"
}
```

---

## 17. Common HTTP Status Mapping

| Scenario | Status Code |
|---|---:|
| Validation failed | 400 |
| Invalid request | 400 |
| Missing or invalid token | 401 |
| Authenticated but no permission | 403 |
| Resource not found | 404 |
| Duplicate or conflict | 409 |
| Business rule violation | 400 or 422 |
| Unexpected exception | 500 |

Examples:

```text
User not found            -> 404
Email already exists      -> 409
Invalid order status      -> 400 or 422
Unauthorized request      -> 401
Forbidden request         -> 403
Unhandled exception       -> 500
```

---

## 18. Should All Errors Use HTTP 200?

No.

Avoid this:

```http
200 OK
```

```json
{
  "success": false,
  "message": "User not found."
}
```

This is bad because the HTTP status says success but the response body says failure.

Better:

```http
404 Not Found
```

```json
{
  "success": false,
  "statusCode": 404,
  "message": "User not found."
}
```

Use correct HTTP status codes because they are important for frontend apps, API gateways, monitoring, logs, and integrations.

---

## 19. Global Error Handling vs Try-Catch in Controller

Bad approach:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetUser(int id)
{
    try
    {
        var user = await _userService.GetUserByIdAsync(id);
        return Ok(user);
    }
    catch (Exception ex)
    {
        return StatusCode(500, ex.Message);
    }
}
```

Problems:

```text
Repeated try-catch
Inconsistent error responses
Risk of exposing internal errors
Controller becomes messy
```

Better:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetUser(int id)
{
    var user = await _userService.GetUserByIdAsync(id);
    return Ok(user);
}
```

Global middleware handles exceptions.

---

## 20. Where Does ProblemDetails Fit?

ASP.NET Core has a built-in standard called `ProblemDetails`.

Example:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "User not found.",
  "traceId": "0HN7L9KJ4FQ2M:00000010"
}
```

Many teams prefer `ProblemDetails` because it follows a standard format.

Other teams prefer a custom format:

```json
{
  "success": false,
  "statusCode": 404,
  "message": "User not found.",
  "traceId": "..."
}
```

Both are acceptable depending on project standards.

Interview-safe answer:

> I prefer a consistent global error structure. Depending on project standards, this can be a custom `ApiErrorResponse` or ASP.NET Core's built-in `ProblemDetails`.

---

## 21. Interview-Ready Answer

> In a production Web API, I prefer using a global error response structure so that all errors follow the same format. I usually handle unhandled exceptions using custom exception middleware and return a response containing success, statusCode, message, errors, traceId, and timestamp. For known business errors, I use custom exceptions like NotFoundException or ConflictException and map them to proper HTTP status codes. For unexpected errors, I log the full exception but return a generic safe message to the client. This keeps controllers clean, avoids repeated try-catch blocks, prevents exposing internal details, and makes frontend error handling consistent.

---

## 22. Practical Memory Trick

```text
Controller should focus on request/response.
Service should focus on business logic.
Middleware should handle global exceptions.
Logs should contain technical details.
Client should receive safe consistent errors.
```

---

## 23. Key Takeaways

1. Use a consistent error response format.
2. Do not expose internal exception details to clients.
3. Use global exception middleware instead of repeated controller try-catch.
4. Use custom exceptions for known business errors.
5. Use correct HTTP status codes.
6. Customize validation responses if required.
7. Include `traceId` or `correlationId` for production support.
8. Use `ProblemDetails` if your project follows standard RFC-style error responses.
