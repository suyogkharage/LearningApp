using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.HealthChecks;
using LearningApp.Api.Middleware;
using LearningApp.Api.Repositories;
using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
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
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<MicrosoftAccountService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
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

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        const string learningAppBearerScheme = "LearningAppBearer";

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = learningAppBearerScheme;
            options.DefaultChallengeScheme = learningAppBearerScheme;
            options.DefaultForbidScheme = learningAppBearerScheme;
        });

        authenticationBuilder.AddPolicyScheme(learningAppBearerScheme, null, options =>
        {
            options.ForwardDefaultSelector = context => SelectBearerScheme(context, jwtSettings);
        });

        authenticationBuilder.AddMicrosoftIdentityWebApi(options =>
            {
                azureAdSection.Bind(options);
                options.TokenValidationParameters.ValidAudiences = new[]
                {
                    azureAdAudience,
                    azureAdClientId
                }.Where(value => !string.IsNullOrWhiteSpace(value));

                options.Events ??= new JwtBearerEvents();
                var previousTokenValidated = options.Events.OnTokenValidated;
                options.Events.OnTokenValidated = async context =>
                {
                    await previousTokenValidated(context);
                    if (context.Result is not null || context.Principal is null) return;

                    var accounts = context.HttpContext.RequestServices.GetRequiredService<MicrosoftAccountService>();
                    var principal = await accounts.ResolveAsync(context.Principal, context.HttpContext.RequestAborted);
                    if (principal is null)
                    {
                        context.Fail("A delegated access_as_user token and an active LearningApp account are required.");
                        return;
                    }

                    context.Principal = principal;
                };
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

        if (!string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
        {
            authenticationBuilder.AddJwtBearer("LocalJwt", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    NameClaimType = System.Security.Claims.ClaimTypes.Name,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var response = CreateErrorResponse(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "You are not authenticated.");

                        await context.Response.WriteAsJsonAsync(response);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var response = CreateErrorResponse(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "You do not have permission to access this resource.");

                        await context.Response.WriteAsJsonAsync(response);
                    }
                };
            });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            });

            options.AddPolicy("LearnerOrAdmin", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Learner", "Admin");
            });
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

    private static string SelectBearerScheme(HttpContext context, JwtSettings jwtSettings)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(jwtSettings.Issuer)
            || string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return string.Equals(jwt.Issuer, jwtSettings.Issuer, StringComparison.Ordinal)
                ? "LocalJwt"
                : JwtBearerDefaults.AuthenticationScheme;
        }
        catch
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
    }

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
