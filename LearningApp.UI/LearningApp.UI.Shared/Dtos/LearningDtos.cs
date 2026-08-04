using System.ComponentModel.DataAnnotations;

namespace LearningApp.UI.Shared.Dtos;

public sealed record UserDto(Guid UserId, string Email, string FullName, string Role, bool IsActive, DateTime CreatedUtc);

public sealed class UserCreateRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = "Learner";
}

public sealed class UserUpdateRequest
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Role { get; set; } = "Learner";

    public bool IsActive { get; set; } = true;
}

public sealed record CategoryDto(Guid CategoryId, string Name, string? Description, bool IsActive, DateTime CreatedUtc);

public sealed class CategoryUpsertRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record CourseDto(
    Guid CourseId,
    Guid CategoryId,
    string CategoryName,
    string Title,
    string? Description,
    string DifficultyLevel,
    int DurationHours,
    bool IsPublished,
    DateTime CreatedUtc);

public sealed class CourseUpsertRequest
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required, MaxLength(30)]
    public string DifficultyLevel { get; set; } = "Beginner";

    [Range(1, 500)]
    public int DurationHours { get; set; } = 1;

    public bool IsPublished { get; set; }
}

public sealed record EnrollmentDto(
    Guid EnrollmentId,
    Guid UserId,
    string UserName,
    Guid CourseId,
    string CourseTitle,
    DateTime EnrolledUtc,
    string Status,
    DateTime? CompletedUtc);

public sealed class EnrollmentCreateRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid CourseId { get; set; }
}

public sealed class EnrollmentUpdateRequest
{
    [Required, MaxLength(30)]
    public string Status { get; set; } = "Active";

    public DateTime? CompletedUtc { get; set; }
}

public sealed record LearningProgressDto(
    Guid LearningProgressId,
    Guid EnrollmentId,
    decimal PercentComplete,
    DateTime? LastAccessedUtc,
    string? Notes,
    DateTime UpdatedUtc);

public sealed class LearningProgressUpsertRequest
{
    [Required]
    public Guid EnrollmentId { get; set; }

    [Range(0, 100)]
    public decimal PercentComplete { get; set; }

    public DateTime? LastAccessedUtc { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
