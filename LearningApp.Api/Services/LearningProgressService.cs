using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class LearningProgressService(
    LearningAppDbContext dbContext,
    ICurrentUser currentUser,
    IValidator<LearningProgressUpsertRequest> validator) : ILearningProgressService
{
    public async Task<IReadOnlyList<LearningProgressDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await VisibleProgress().AsNoTracking().OrderByDescending(x => x.UpdatedUtc).Select(x => x.ToDto()).ToListAsync(cancellationToken);

    public async Task<LearningProgressDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await VisibleProgress().AsNoTracking().FirstOrDefaultAsync(x => x.LearningProgressId == id, cancellationToken))?.ToDto()
        ?? throw new NotFoundException("Learning progress was not found.");

    public async Task<LearningProgressDto> UpsertAsync(LearningProgressUpsertRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(validator.Validate(request));
        if (!await VisibleEnrollments().AnyAsync(x => x.EnrollmentId == request.EnrollmentId, cancellationToken))
        {
            throw new ValidationAppException(new[] { "EnrollmentId does not reference an existing enrollment." });
        }

        var entity = await VisibleProgress().FirstOrDefaultAsync(x => x.EnrollmentId == request.EnrollmentId, cancellationToken);
        if (entity is null)
        {
            entity = new LearningProgress { LearningProgressId = Guid.NewGuid(), EnrollmentId = request.EnrollmentId };
            dbContext.LearningProgress.Add(entity);
        }

        entity.PercentComplete = request.PercentComplete;
        entity.LastAccessedUtc = request.LastAccessedUtc;
        entity.Notes = request.Notes;
        entity.UpdatedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await VisibleProgress().FirstOrDefaultAsync(x => x.LearningProgressId == id, cancellationToken)
            ?? throw new NotFoundException("Learning progress was not found.");
        dbContext.LearningProgress.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<LearningProgress> VisibleProgress()
    {
        if (currentUser.IsAdmin)
        {
            return dbContext.LearningProgress;
        }

        return currentUser.UserId is { } userId
            ? dbContext.LearningProgress.Where(x => x.Enrollment.UserId == userId)
            : dbContext.LearningProgress.Where(_ => false);
    }

    private IQueryable<Enrollment> VisibleEnrollments()
    {
        if (currentUser.IsAdmin)
        {
            return dbContext.Enrollments;
        }

        return currentUser.UserId is { } userId
            ? dbContext.Enrollments.Where(x => x.UserId == userId)
            : dbContext.Enrollments.Where(_ => false);
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0) throw new ValidationAppException(errors);
    }
}
