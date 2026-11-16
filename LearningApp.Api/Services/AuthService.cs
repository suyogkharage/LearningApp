using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class AuthService(
    LearningAppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IValidator<LoginRequest> loginValidator,
    IValidator<RegisterRequest> registerValidator) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var errors = loginValidator.Validate(request);
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

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = registerValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = "Learner",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return jwtTokenService.CreateToken(user);
    }
}
