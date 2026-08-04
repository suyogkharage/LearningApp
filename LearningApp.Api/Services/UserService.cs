using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class UserService(
    LearningAppDbContext dbContext,
    IPasswordHasher passwordHasher,
    IValidator<UserCreateRequest> createValidator,
    IValidator<UserUpdateRequest> updateValidator) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking().OrderBy(x => x.FullName).Select(x => x.ToDto()).ToListAsync(cancellationToken);

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id, cancellationToken))?.ToDto()
        ?? throw new NotFoundException("User was not found.");

    public async Task<UserDto> CreateAsync(UserCreateRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(createValidator.Validate(request));
        if (await dbContext.Users.AnyAsync(x => x.Email == request.Email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var entity = new User
        {
            UserId = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = request.Role.Trim(),
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(updateValidator.Validate(request));
        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.UserId == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        entity.FullName = request.FullName.Trim();
        entity.Role = request.Role.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.UserId == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");
        dbContext.Users.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }
}
