using System.Security.Claims;
using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Authentication;

// Called only after Microsoft's bearer token has passed signature, issuer,
// audience and lifetime validation. Entra supplies roles; the DB supplies the
// internal user ID, profile and local active status.
public sealed class MicrosoftAccountService(LearningAppDbContext dbContext)
{
    public const string UserIdClaim = "learningapp_user_id";

    public async Task<ClaimsPrincipal?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var issuer = principal.FindFirstValue("iss");
        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var scopes = principal.FindFirstValue("scp")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/scope");

        // Require a delegated API token, never an ID token or app-only token.
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 200
            || string.IsNullOrWhiteSpace(subject) || subject.Length > 200
            || scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("access_as_user", StringComparer.Ordinal) != true)
        {
            return null;
        }

        var login = await FindLoginAsync(issuer, subject, cancellationToken);
        if (login is null)
        {
            var name = principal.FindFirstValue("name") ?? principal.Identity?.Name ?? "Microsoft user";
            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = name[..Math.Min(150, name.Length)],
                // Email is display/contact data, never an account linking key.
                // A reserved address avoids collisions with password accounts.
                Email = $"microsoft-{Guid.NewGuid():N}@identity.invalid",
                PasswordHash = string.Empty,
                Role = "Learner", // Used only for local password login, never Microsoft authorization.
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            };
            var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email")
                ?? principal.FindFirstValue("preferred_username");
            if (!string.IsNullOrWhiteSpace(email) && email.Length <= 256
                && !await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
            {
                user.Email = email;
            }

            login = new ExternalLogin
            {
                ExternalLoginId = Guid.NewGuid(), Issuer = issuer, Subject = subject, User = user
            };
            dbContext.ExternalLogins.Add(login);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Parallel dashboard requests can provision the same identity.
                dbContext.ChangeTracker.Clear();
                login = await FindLoginAsync(issuer, subject, cancellationToken);
                if (login is null) throw;
            }
        }

        if (!login.User.IsActive) return null;

        // Normalize mapped/unmapped Entra access-token roles. Never add a role
        // from Users.Role or infer Learner merely because sign-in succeeded.
        var roles = principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("roles"))
            .Select(c => c.Value).Distinct(StringComparer.Ordinal);
        var identity = new ClaimsIdentity(principal.Claims.Where(c =>
                c.Type != ClaimTypes.Role && c.Type != "roles" && c.Type != UserIdClaim),
            principal.Identity?.AuthenticationType ?? "Microsoft", "name", ClaimTypes.Role);
        identity.AddClaim(new Claim(UserIdClaim, login.UserId.ToString()));
        identity.AddClaims(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(identity);
    }

    private Task<ExternalLogin?> FindLoginAsync(string issuer, string subject, CancellationToken cancellationToken) =>
        dbContext.ExternalLogins.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject, cancellationToken);
}
