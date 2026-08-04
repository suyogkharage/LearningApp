using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class LearningProgressService(LearningAppDbContext dbContext, IValidator<LearningProgressUpsertRequest> validator) : ILearningProgressService
{
    public async Task<IReadOnlyList<LearningProgressDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.LearningProgress.AsNoTracking().OrderByDescending(x => x.UpdatedUtc).Select(x => x.ToDto()).ToListAsync(cancellationToken);

    public async Task<LearningProgressDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await dbContext.LearningProgress.AsNoTracking().FirstOrDefaultAsync(x => x.LearningProgressId == id, cancellationToken))?.ToDto()
        ?? throw new NotFoundException("Learning progress was not found.");

    public async Task<LearningProgressDto> UpsertAsync(LearningProgressUpsertRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(validator.Validate(request));
        if (!await dbContext.Enrollments.AnyAsync(x => x.EnrollmentId == request.EnrollmentId, cancellationToken))
        {
            throw new ValidationAppException(new[] { "EnrollmentId does not reference an existing enrollment." });
        }

        var entity = await dbContext.LearningProgress.FirstOrDefaultAsync(x => x.EnrollmentId == request.EnrollmentId, cancellationToken);
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
        var entity = await dbContext.LearningProgress.FirstOrDefaultAsync(x => x.LearningProgressId == id, cancellationToken)
            ?? throw new NotFoundException("Learning progress was not found.");
        dbContext.LearningProgress.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ThrowIfInvalid(IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0) throw new ValidationAppException(errors);
    }
}
