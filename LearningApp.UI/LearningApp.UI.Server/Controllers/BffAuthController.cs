using LearningApp.UI.Shared.Auth;
using LearningApp.UI.Shared.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace LearningApp.UI.Server.Controllers;

[ApiController]
[Route("bff")]
public sealed class BffAuthController(IHttpClientFactory httpClientFactory) : ControllerBase
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
    public async Task<ActionResult<BffUserInfo>> GetUser(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new BffUserInfo(false, null, null, null, Array.Empty<string>()));
        }

        // API app roles belong to the access token, not the UI's ID token.
        // Ask the API for its effective roles and our internal account ID.
        if (User.FindFirstValue("auth_source") != "local")
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrWhiteSpace(accessToken)) return Unauthorized();

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var apiClient = httpClientFactory.CreateClient("LearningApi");
            using var response = await apiClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);

            var profile = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileDto>>(cancellationToken: cancellationToken);
            if (profile is not { Success: true, Data: not null }) return StatusCode(StatusCodes.Status502BadGateway);
            return Ok(new BffUserInfo(true, profile.Data.UserId, profile.Data.FullName, profile.Data.Email, profile.Data.Roles));
        }

        var roles = User.FindAll(ClaimTypes.Role)
            .Concat(User.FindAll("roles"))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : (Guid?)null;
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username");

        return Ok(new BffUserInfo(true, userId, User.Identity.Name, email, roles));
    }

    [HttpPost("local-login")]
    [AllowAnonymous]
    public async Task<ActionResult<BffUserInfo>> LocalLogin(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await SendAuthRequestAsync("api/auth/login", request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(ApiResponse<object>.Fail(response.Message, response.Errors));
        }

        await SignInLocalUserAsync(response.Data);
        return Ok(ToBffUserInfo(response.Data.User));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<BffUserInfo>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await SendAuthRequestAsync("api/auth/register", request, cancellationToken);
        if (!response.Success || response.Data is null)
        {
            return BadRequest(ApiResponse<object>.Fail(response.Message, response.Errors));
        }

        await SignInLocalUserAsync(response.Data);
        return Ok(ToBffUserInfo(response.Data.User));
    }

    private async Task<ApiResponse<LoginResponse>> SendAuthRequestAsync<TRequest>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var apiClient = httpClientFactory.CreateClient("LearningApi");
        using var response = await apiClient.PostAsJsonAsync(path, request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(cancellationToken: cancellationToken);

        if (body is not null)
        {
            return body;
        }

        return ApiResponse<LoginResponse>.Fail($"The API returned HTTP {(int)response.StatusCode}.");
    }

    private async Task SignInLocalUserAsync(LoginResponse login)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.User.UserId.ToString()),
            new(ClaimTypes.Email, login.User.Email),
            new(ClaimTypes.Name, login.User.FullName),
            new(ClaimTypes.Role, login.User.Role),
            new("auth_source", "local")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = new DateTimeOffset(DateTime.SpecifyKind(login.ExpiresUtc, DateTimeKind.Utc))
        };

        properties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = login.AccessToken },
            new AuthenticationToken { Name = "expires_at", Value = login.ExpiresUtc.ToString("O") }
        });

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    private static BffUserInfo ToBffUserInfo(UserSummaryDto user) =>
        new(true, user.UserId, user.FullName, user.Email, new[] { user.Role });

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Relative, out var relativeUri)
        && !relativeUri.IsAbsoluteUri
        && url.StartsWith('/');
}
