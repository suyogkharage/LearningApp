using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.HealthChecks;
using LearningApp.Api.Middleware;
using LearningApp.Api.Repositories;
using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using System.Text.Json;

namespace LearningApp.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLearningApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LearningAppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("LearningAppDb")));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IValidator<>), typeof(DataAnnotationsValidator<>));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<ILearningProgressService, LearningProgressService>();

        services.AddHealthChecks()
            .AddCheck<SqlDatabaseHealthCheck>(
                "sql_database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready", "database", "sql" });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .SelectMany(entry => entry.Value!.Errors)
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The request is invalid."
                        : error.ErrorMessage)
                    .ToArray();

                var response = CreateErrorResponse(
                    context.HttpContext,
                    StatusCodes.Status400BadRequest,
                    "Validation failed.",
                    errors);

                return new BadRequestObjectResult(response);
            };
        });

        var azureAdSection = configuration.GetSection("AzureAd");
        var azureAdClientId = azureAdSection["ClientId"];
        var azureAdAudience = azureAdSection["Audience"];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(options =>
            {
                azureAdSection.Bind(options);
                options.TokenValidationParameters.ValidAudiences = new[]
                {
                    azureAdAudience,
                    azureAdClientId
                }.Where(value => !string.IsNullOrWhiteSpace(value));

                options.Events ??= new JwtBearerEvents();
                options.Events.OnChallenge = async context =>
                {
                    context.HandleResponse();

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var response = CreateErrorResponse(
                        context.HttpContext,
                        StatusCodes.Status401Unauthorized,
                        "You are not authenticated.");

                    await context.Response.WriteAsJsonAsync(response);
                };
                options.Events.OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var response = CreateErrorResponse(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "You do not have permission to access this resource.");

                    await context.Response.WriteAsJsonAsync(response);
                };
            }, options => azureAdSection.Bind(options));

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("LearnerOrAdmin", policy => policy.RequireAuthenticatedUser());
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "LearningApp.Api", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseLearningAppExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();

    public static IEndpointRouteBuilder MapLearningAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthCheckResponseAsync
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponseAsync
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponseAsync
        }).AllowAnonymous();

        return endpoints;
    }

    private static ApiErrorResponse CreateErrorResponse(
        HttpContext context,
        int statusCode,
        string message,
        IEnumerable<string>? errors = null) =>
        ApiErrorResponse.Create(
            statusCode,
            message,
            errors,
            context.TraceIdentifier,
            context.Request.Headers["X-Correlation-Id"].FirstOrDefault());

    private static Task WriteHealthCheckResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
