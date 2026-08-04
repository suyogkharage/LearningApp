using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class CourseService(LearningAppDbContext dbContext, IValidator<CourseUpsertRequest> validator) : ICourseService
{
    public async Task<IReadOnlyList<CourseDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Courses.AsNoTracking().Include(x => x.Category).OrderBy(x => x.Title).Select(x => x.ToDto()).ToListAsync(cancellationToken);

    public async Task<CourseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await dbContext.Courses.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.CourseId == id, cancellationToken))?.ToDto()
        ?? throw new NotFoundException("Course was not found.");

    public async Task<CourseDto> CreateAsync(CourseUpsertRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(validator.Validate(request));
        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        var entity = new Course
        {
            CourseId = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description,
            DifficultyLevel = request.DifficultyLevel.Trim(),
            DurationHours = request.DurationHours,
            IsPublished = request.IsPublished,
            CreatedUtc = DateTime.UtcNow
        };
        dbContext.Courses.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.CourseId, cancellationToken);
    }

    public async Task<CourseDto> UpdateAsync(Guid id, CourseUpsertRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(validator.Validate(request));
        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        var entity = await dbContext.Courses.FirstOrDefaultAsync(x => x.CourseId == id, cancellationToken)
            ?? throw new NotFoundException("Course was not found.");
        entity.CategoryId = request.CategoryId;
        entity.Title = request.Title.Trim();
        entity.Description = request.Description;
        entity.DifficultyLevel = request.DifficultyLevel.Trim();
        entity.DurationHours = request.DurationHours;
        entity.IsPublished = request.IsPublished;
        entity.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Courses.FirstOrDefaultAsync(x => x.CourseId == id, cancellationToken)
            ?? throw new NotFoundException("Course was not found.");
        dbContext.Courses.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Categories.AnyAsync(x => x.CategoryId == categoryId, cancellationToken))
        {
            throw new ValidationAppException(new[] { "CategoryId does not reference an existing category." });
        }
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0) throw new ValidationAppException(errors);
    }
}
