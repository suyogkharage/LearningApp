using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class AuthService(
    LearningAppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IValidator<LoginRequest> validator) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == request.Email, cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationAppException(new[] { "Invalid email or password." });
        }

        return jwtTokenService.CreateToken(user);
    }
}
