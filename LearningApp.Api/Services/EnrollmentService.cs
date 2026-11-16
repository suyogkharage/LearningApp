using LearningApp.Api.Authentication;
using LearningApp.Api.Data;
using LearningApp.Api.Entities;
using LearningApp.Api.Exceptions;
using LearningApp.UI.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LearningApp.Api.Services;

public sealed class EnrollmentService(
    LearningAppDbContext dbContext,
    ICurrentUser currentUser,
    IValidator<EnrollmentCreateRequest> createValidator,
    IValidator<EnrollmentUpdateRequest> updateValidator) : IEnrollmentService
{
    public async Task<IReadOnlyList<EnrollmentDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await VisibleEnrollments().AsNoTracking().Include(x => x.User).Include(x => x.Course)
            .OrderByDescending(x => x.EnrolledUtc).Select(x => x.ToDto()).ToListAsync(cancellationToken);

    public async Task<EnrollmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await VisibleEnrollments().AsNoTracking().Include(x => x.User).Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.EnrollmentId == id, cancellationToken))?.ToDto()
        ?? throw new NotFoundException("Enrollment was not found.");

    public async Task<EnrollmentDto> CreateAsync(EnrollmentCreateRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(createValidator.Validate(request));
        var userId = currentUser.IsAdmin
            ? request.UserId
            : currentUser.UserId ?? throw new ValidationAppException(new[] { "Your account is not linked to an internal LearningApp user." });

        if (!await dbContext.Users.AnyAsync(x => x.UserId == userId && x.IsActive, cancellationToken))
        {
            throw new ValidationAppException(new[] { "UserId does not reference an active user." });
        }

        if (!await dbContext.Courses.AnyAsync(x => x.CourseId == request.CourseId, cancellationToken))
        {
            throw new ValidationAppException(new[] { "CourseId does not reference an existing course." });
        }

        if (await dbContext.Enrollments.AnyAsync(x => x.UserId == userId && x.CourseId == request.CourseId, cancellationToken))
        {
            throw new ConflictException("The selected user is already enrolled in this course.");
        }

        var entity = new Enrollment
        {
            EnrollmentId = Guid.NewGuid(),
            UserId = userId,
            CourseId = request.CourseId,
            Status = "Active",
            EnrolledUtc = DateTime.UtcNow
        };

        dbContext.Enrollments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.EnrollmentId, cancellationToken);
    }

    public async Task<EnrollmentDto> UpdateAsync(Guid id, EnrollmentUpdateRequest request, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(updateValidator.Validate(request));
        var entity = await VisibleEnrollments().FirstOrDefaultAsync(x => x.EnrollmentId == id, cancellationToken)
            ?? throw new NotFoundException("Enrollment was not found.");
        entity.Status = request.Status.Trim();
        entity.CompletedUtc = request.CompletedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await VisibleEnrollments().FirstOrDefaultAsync(x => x.EnrollmentId == id, cancellationToken)
            ?? throw new NotFoundException("Enrollment was not found.");
        dbContext.Enrollments.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
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
