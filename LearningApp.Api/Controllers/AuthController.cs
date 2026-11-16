using LearningApp.UI.Shared.Common;
using LearningApp.Api.Services;
using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.UI.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LearningApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me(
        [FromServices] LearningAppDbContext dbContext,
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == currentUser.UserId && x.IsActive, cancellationToken);
        if (user is null) return Unauthorized();

        var roles = User.FindAll(ClaimTypes.Role).Concat(User.FindAll("roles"))
            .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
        return Ok(ApiResponse<UserProfileDto>.Ok(new UserProfileDto(user.UserId, user.Email, user.FullName, roles)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LoginResponse>.Ok(await authService.LoginAsync(request, cancellationToken), "Signed in."));

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LoginResponse>.Ok(await authService.RegisterAsync(request, cancellationToken), "Account created."));
}
