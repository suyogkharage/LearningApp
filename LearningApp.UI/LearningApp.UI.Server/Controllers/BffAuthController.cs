using LearningApp.UI.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LearningApp.UI.Server.Controllers;

[ApiController]
[Route("bff")]
public sealed class BffAuthController : ControllerBase
{
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = "/")
    {
        var redirectUri = IsLocalUrl(returnUrl) ? returnUrl! : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUri },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/login" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("user")]
    [AllowAnonymous]
    public ActionResult<BffUserInfo> GetUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new BffUserInfo(false, null, Array.Empty<string>()));
        }

        var roles = User.FindAll(ClaimTypes.Role)
            .Concat(User.FindAll("roles"))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new BffUserInfo(true, User.Identity.Name, roles));
    }

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Relative, out var relativeUri)
        && !relativeUri.IsAbsoluteUri
        && url.StartsWith('/');
}
