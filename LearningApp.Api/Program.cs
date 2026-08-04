using System.Threading.RateLimiting;
using LearningApp.Api.Extensions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddLearningApp(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("LearningAppUi", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// fixed window rate limiting with 10 requests per minute limit and no queueing
// builder.Services.AddRateLimiter(options =>
// {
//     options.AddFixedWindowLimiter("fixed", limiterOptions =>
//     {
//         limiterOptions.PermitLimit = 10;
//         limiterOptions.Window = TimeSpan.FromMinutes(1);
//         limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
//         limiterOptions.QueueLimit = 0;
//     });
// });

// rate limimting by IP address with 100 requests per minute limit and no queueing
// builder.Services.AddRateLimiter(options =>
// {
//     options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
//         httpContext =>
//             RateLimitPartition.GetFixedWindowLimiter(
//                 partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
//                 factory: _ => new FixedWindowRateLimiterOptions
//                 {
//                     PermitLimit = 100,
//                     Window = TimeSpan.FromMinutes(1),
//                     QueueLimit = 0
//                 }));
//     options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;;
// });

// Previous global rate limiter:
// This applied the limit to every API endpoint for the same user/IP partition.
// It is commented out because the dashboard calls multiple APIs on load, so a
// small permit limit could throttle unrelated endpoints very quickly.
//
// builder.Services.AddRateLimiter(options =>
// {
//     options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
//         httpContext =>
//         {
//             var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
//             var partitionKey = userId
//                                 ?? httpContext.Connection.RemoteIpAddress?.ToString()
//                                 ?? "unknown";
//
//             return RateLimitPartition.GetFixedWindowLimiter(
//                 partitionKey: partitionKey,
//                 factory: _ => new FixedWindowRateLimiterOptions
//                 {
//                     PermitLimit = 5,
//                     Window = TimeSpan.FromMinutes(1),
//                     QueueLimit = 0
//                 });
//         });
//     options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
//     options.OnRejected = async (context, cancellationToken) =>
//     {
//         context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
//         context.HttpContext.Response.ContentType = "application/json";
//         var response = new
//         {
//             success = false,
//             statusCode = StatusCodes.Status429TooManyRequests,
//             message = "Too many requests. Please try again later.",
//             traceId = context.HttpContext.TraceIdentifier,
//             correlationId = context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
//         };
//
//         await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
//     };
// });

// Rate limiting for only the dashboard read endpoints:
// GET /api/categories
// GET /api/courses
// GET /api/enrollments
// GET /api/learningprogress
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("dashboard-reads", httpContext =>
    {
        var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = userId 
                            ?? httpContext.Connection.RemoteIpAddress?.ToString() 
                            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 500,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        var response = new
        {
            success = false,
            statusCode = StatusCodes.Status429TooManyRequests,
            message = "Too many requests. Please try again later.",
            traceId = context.HttpContext.TraceIdentifier,
            correlationId = context.HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        };

        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };
    
});

var app = builder.Build();


app.UseLearningAppExceptionHandling();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("LearningAppUi");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers()
    .Add(endpointBuilder =>
    {
        var action = endpointBuilder.Metadata.OfType<ControllerActionDescriptor>().FirstOrDefault();
        if (action is null)
        {
            return;
        }

        var isDashboardReadEndpoint =
            action.ActionName == "GetAll" &&
            action.ControllerName is "Categories" or "Courses" or "Enrollments" or "LearningProgress";

        if (isDashboardReadEndpoint)
        {
            endpointBuilder.Metadata.Add(new EnableRateLimitingAttribute("dashboard-reads"));
        }
    });
app.MapLearningAppHealthChecks();
app.Run();
