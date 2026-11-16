using System.ComponentModel.DataAnnotations;

namespace LearningApp.UI.Shared.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresUtc,
    UserSummaryDto User);

public sealed record UserSummaryDto(
    Guid UserId,
    string Email,
    string FullName,
    string Role);

public sealed record UserProfileDto(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

public sealed record BffUserInfo(
    bool IsAuthenticated,
    Guid? UserId,
    string? Name,
    string? Email,
    IReadOnlyList<string> Roles);
