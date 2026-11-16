using System.Security.Claims;
using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Extensions;
using LearningApp.Api.Controllers;
using LearningApp.Api.Services;
using LearningApp.UI.Shared.Auth;
using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
var dbOptions = new DbContextOptionsBuilder<LearningAppDbContext>().UseSqlite(connection).Options;
await using var db = new LearningAppDbContext(dbOptions);
await db.Database.EnsureCreatedAsync();

var services = new ServiceCollection();
services.AddLogging();
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
    ["AzureAd:TenantId"] = "22222222-2222-2222-2222-222222222222",
    ["AzureAd:ClientId"] = "11111111-1111-1111-1111-111111111111"
}).Build();
services.AddSingleton<IConfiguration>(configuration);
services.AddLearningApp(configuration);
// Use a real relational DB for provisioning, with no external services.
services.AddSingleton(db);
await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer");
var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
var count = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    count++;
}

ClaimsPrincipal MicrosoftUser(string subject = "entra-subject", string? delegatedScope = "access_as_user",
    string issuer = "https://login.microsoftonline.com/22222222-2222-2222-2222-222222222222/v2.0",
    string[]? roles = null, string roleClaimType = "roles")
{
    var claims = new List<Claim>
    {
        new("iss", issuer), new(ClaimTypes.NameIdentifier, subject),
        new("name", "Entra user"), new(ClaimTypes.Email, "same@example.com")
    };
    claims.AddRange((roles ?? new[] { "Learner" }).Select(role => new Claim(roleClaimType, role)));
    if (delegatedScope is not null) claims.Add(new("scp", delegatedScope));
    return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
}

async Task<TokenValidatedContext> Validate(ClaimsPrincipal principal)
{
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    var context = new TokenValidatedContext(http, new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), options)
    {
        Principal = principal
    };
    // Exercise the registered hook after the bearer middleware validates a token.
    await options.Events.TokenValidated(context);
    return context;
}

var first = await Validate(MicrosoftUser());
Check(first.Result?.Failure is null, "Valid Microsoft account must resolve.");
var learner = first.Principal!;
var userId = Guid.Parse(learner.FindFirstValue(MicrosoftAccountService.UserIdClaim)!);
Check(learner.IsInRole("Learner") && !learner.IsInRole("Admin"), "Entra Learner role must be respected.");
Check((await authorization.AuthorizeAsync(learner, null, "LearnerOrAdmin")).Succeeded, "Learner must pass dashboard policy.");
Check(!(await authorization.AuthorizeAsync(learner, null, "AdminOnly")).Succeeded, "Learner must fail admin policy.");
Check(await db.Users.CountAsync() == 1 && await db.ExternalLogins.CountAsync() == 1, "First sign-in must persist a user and external link.");
var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = learner } };
Check(new CurrentUser(accessor).UserId == userId, "Enrollment ownership must use internal ID, not Microsoft sub.");
var again = await Validate(MicrosoftUser());
Check(again.Principal!.FindFirstValue(MicrosoftAccountService.UserIdClaim) == userId.ToString() && await db.Users.CountAsync() == 1,
    "Repeat sign-in must reuse the same account.");

var user = await db.Users.SingleAsync(x => x.UserId == userId);
Check(!new Pbkdf2PasswordHasher().Verify("anything", user.PasswordHash), "External account must not allow password login.");
user.Role = "Admin";
await db.SaveChangesAsync();
var stillLearner = await Validate(MicrosoftUser());
Check(!(await authorization.AuthorizeAsync(stillLearner.Principal!, null, "AdminOnly")).Succeeded,
    "Changing the database role must never elevate a Microsoft user.");
Check(!new CurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = stillLearner.Principal! } }).IsAdmin,
    "Enrollment/progress admin visibility must use Entra roles, not DB role.");
user.Role = "Learner";
await db.SaveChangesAsync();
var admin = await Validate(MicrosoftUser(roles: new[] { "Admin" }));
Check((await authorization.AuthorizeAsync(admin.Principal!, null, "AdminOnly")).Succeeded,
    "Entra Admin must pass even when the database role is Learner.");
Check(admin.Principal!.FindFirstValue(MicrosoftAccountService.UserIdClaim) == userId.ToString(),
    "Role changes must preserve the enrollment/progress user ID.");
var demoted = await Validate(MicrosoftUser());
Check(!demoted.Principal!.IsInRole("Admin"), "A newly issued Learner token must remove Admin access.");
var mappedAdmin = await Validate(MicrosoftUser(roles: new[] { "Admin" }, roleClaimType: ClaimTypes.Role));
Check(mappedAdmin.Principal!.IsInRole("Admin"), "Mapped Entra role claims must work.");
var both = await Validate(MicrosoftUser(roles: new[] { "Learner", "Admin", "Admin" }));
Check(both.Principal!.FindAll(ClaimTypes.Role).Count() == 2 && both.Principal.IsInRole("Admin") && both.Principal.IsInRole("Learner"),
    "All assigned roles must survive normalization without duplicates.");

async Task<UserProfileDto> Profile(ClaimsPrincipal principal)
{
    var httpAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
    var controller = new AuthController(scope.ServiceProvider.GetRequiredService<IAuthService>())
    {
        ControllerContext = new ControllerContext { HttpContext = httpAccessor.HttpContext }
    };
    var response = await controller.Me(db, new CurrentUser(httpAccessor), CancellationToken.None);
    return ((ApiResponse<UserProfileDto>)((OkObjectResult)response.Result!).Value!).Data!;
}

var adminProfile = await Profile(admin.Principal!);
Check(adminProfile.UserId == userId && adminProfile.Roles.SequenceEqual(new[] { "Admin" }),
    "Profile must report Entra Admin, not the database Learner role.");
Check((await Profile(both.Principal!)).Roles.Count == 2, "Profile must supply all roles to the UI.");
user.Role = "Admin";
await db.SaveChangesAsync();
var roleless = await Validate(MicrosoftUser(roles: Array.Empty<string>()));
Check(roleless.Result?.Failure is null, "Valid identity with no app role should remain authenticated.");
Check(!(await authorization.AuthorizeAsync(roleless.Principal!, null, "LearnerOrAdmin")).Succeeded
    && !(await authorization.AuthorizeAsync(roleless.Principal!, null, "AdminOnly")).Succeeded,
    "A roleless Microsoft token must fail both policies despite a database Admin role.");
Check((await Profile(roleless.Principal!)).Roles.Count == 0, "UI must not receive a database role fallback.");
var unassigned = await Validate(MicrosoftUser("unassigned", roles: Array.Empty<string>()));
Check(!unassigned.Principal!.IsInRole("Learner"), "First-time unassigned users must never be granted Learner automatically.");
var unknownRole = await Validate(MicrosoftUser("unknown-role", roles: new[] { "Reader" }));
Check(!(await authorization.AuthorizeAsync(unknownRole.Principal!, null, "LearnerOrAdmin")).Succeeded,
    "An unrelated Entra role must not grant dashboard access.");
user.IsActive = false;
await db.SaveChangesAsync();
Check((await Validate(MicrosoftUser())).Result?.Failure is not null, "Disabled account must fail authentication.");

var other = await Validate(MicrosoftUser("other-subject"));
Check(other.Result?.Failure is null && other.Principal!.IsInRole("Learner"), "Same email must never inherit an existing user's Admin role.");
Check(other.Principal!.FindFirstValue(MicrosoftAccountService.UserIdClaim) != userId.ToString(), "Same email must not link accounts.");
var differentIssuer = await Validate(MicrosoftUser(issuer: "https://another.validated.issuer/v2.0"));
Check(differentIssuer.Principal!.FindFirstValue(MicrosoftAccountService.UserIdClaim) != userId.ToString(), "Issuer is part of the account key.");
var usersBefore = await db.Users.CountAsync();
Check((await Validate(MicrosoftUser("no-scope", null))).Result?.Failure is not null, "ID/app-only token must fail.");
Check((await Validate(MicrosoftUser("wrong-scope", "other_access"))).Result?.Failure is not null, "Wrong delegated scope must fail.");
Check(await db.Users.CountAsync() == usersBefore, "Rejected tokens must not provision users.");

// Verify that retained local IDs still protect persisted learning data, even
// when the database role disagrees with the role in the Entra token.
user.IsActive = true;
var category = new Category { CategoryId = Guid.NewGuid(), Name = "Auth checks", CreatedUtc = DateTime.UtcNow };
var course = new Course
{
    CourseId = Guid.NewGuid(), Category = category, Title = "Ownership checks",
    DurationHours = 1, CreatedUtc = DateTime.UtcNow
};
var ownEnrollment = new Enrollment
{
    EnrollmentId = Guid.NewGuid(), UserId = userId, Course = course, EnrolledUtc = DateTime.UtcNow
};
var otherEnrollment = new Enrollment
{
    EnrollmentId = Guid.NewGuid(),
    UserId = Guid.Parse(other.Principal!.FindFirstValue(MicrosoftAccountService.UserIdClaim)!),
    Course = course, EnrolledUtc = DateTime.UtcNow
};
db.LearningProgress.AddRange(
    new LearningProgress { LearningProgressId = Guid.NewGuid(), Enrollment = ownEnrollment, PercentComplete = 25, UpdatedUtc = DateTime.UtcNow },
    new LearningProgress { LearningProgressId = Guid.NewGuid(), Enrollment = otherEnrollment, PercentComplete = 50, UpdatedUtc = DateTime.UtcNow });
await db.SaveChangesAsync();
var requestAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
requestAccessor.HttpContext = new DefaultHttpContext { User = demoted.Principal! };
var enrollments = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
var progress = scope.ServiceProvider.GetRequiredService<ILearningProgressService>();
Check((await enrollments.GetAllAsync(CancellationToken.None)).Single().EnrollmentId == ownEnrollment.EnrollmentId,
    "Entra Learner must see existing own enrollment only, despite DB Admin role.");
Check((await progress.GetAllAsync(CancellationToken.None)).Single().EnrollmentId == ownEnrollment.EnrollmentId,
    "Entra Learner must see existing own progress only.");
requestAccessor.HttpContext = new DefaultHttpContext { User = admin.Principal! };
Check((await enrollments.GetAllAsync(CancellationToken.None)).Count == 2, "Entra Admin must see all enrollments.");
Check((await progress.GetAllAsync(CancellationToken.None)).Count == 2, "Entra Admin must see all progress.");

var localId = Guid.NewGuid();
accessor.HttpContext = new DefaultHttpContext();
accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
{
    new Claim(ClaimTypes.NameIdentifier, localId.ToString()), new Claim(ClaimTypes.Role, "Learner")
}, "LocalJwt"));
Check(new CurrentUser(accessor).UserId == localId, "Local password user IDs must remain supported.");
Check((await authorization.AuthorizeAsync(accessor.HttpContext.User, null, "LearnerOrAdmin")).Succeeded, "Local Learner must still pass dashboard policy.");
Console.WriteLine($"Passed {count} authentication regression checks.");
