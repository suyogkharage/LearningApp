using LearningApp.Api.Entities;
using LearningApp.UI.Shared.Dtos;

namespace LearningApp.Api.Services;

public static class MappingExtensions
{
    public static UserDto ToDto(this User entity) => new(entity.UserId, entity.Email, entity.FullName, entity.Role, entity.IsActive, entity.CreatedUtc);
    public static CategoryDto ToDto(this Category entity) => new(entity.CategoryId, entity.Name, entity.Description, entity.IsActive, entity.CreatedUtc);
    public static CourseDto ToDto(this Course entity) => new(entity.CourseId, entity.CategoryId, entity.Category?.Name ?? string.Empty, entity.Title, entity.Description, entity.DifficultyLevel, entity.DurationHours, entity.IsPublished, entity.CreatedUtc);
    public static EnrollmentDto ToDto(this Enrollment entity) => new(entity.EnrollmentId, entity.UserId, entity.User?.FullName ?? string.Empty, entity.CourseId, entity.Course?.Title ?? string.Empty, entity.EnrolledUtc, entity.Status, entity.CompletedUtc);
    public static LearningProgressDto ToDto(this LearningProgress entity) => new(entity.LearningProgressId, entity.EnrollmentId, entity.PercentComplete, entity.LastAccessedUtc, entity.Notes, entity.UpdatedUtc);
}
