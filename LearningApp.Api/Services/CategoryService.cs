using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class CategoryService(LearningAppDbContext dbContext, IValidator<CategoryUpsertRequest> validator) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking().OrderBy(x => x.Name).Select(x => x.ToDto()).ToListAsync(cancellationToken);

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await dbContext.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.CategoryId == id, cancellationToken))?.ToDto()
        ?? throw new NotFoundException("Category was not found.");

    public async Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(validator.Validate(request));
        if (await dbContext.Categories.AnyAsync(x => x.Name == request.Name, cancellationToken))
        {
            throw new ConflictException("A category with this name already exists.");
        }

        var entity = new Category { CategoryId = Guid.NewGuid(), Name = request.Name.Trim(), Description = request.Description, IsActive = request.IsActive, CreatedUtc = DateTime.UtcNow };
        dbContext.Categories.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(validator.Validate(request));
        var entity = await dbContext.Categories.FirstOrDefaultAsync(x => x.CategoryId == id, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Categories.FirstOrDefaultAsync(x => x.CategoryId == id, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        dbContext.Categories.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0) throw new ValidationAppException(errors);
    }
}
