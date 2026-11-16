using System.Security.Claims;

namespace LearningApp.Api.Authentication;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirstValue(MicrosoftAccountService.UserIdClaim)
                ?? user?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user?.FindFirstValue("sub");

            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public bool IsAdmin =>
        httpContextAccessor.HttpContext?.User.IsInRole("Admin") == true;
}
