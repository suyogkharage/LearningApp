using LearningApp.UI.Shared.Auth;
using LearningApp.UI.Shared.Dtos;

namespace LearningApp.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(UserCreateRequest request, CancellationToken cancellationToken);
    Task<UserDto> UpdateAsync(Guid id, UserUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateAsync(Guid id, CategoryUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICourseService
{
    Task<IReadOnlyList<CourseDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<CourseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CourseDto> CreateAsync(CourseUpsertRequest request, CancellationToken cancellationToken);
    Task<CourseDto> UpdateAsync(Guid id, CourseUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IEnrollmentService
{
    Task<IReadOnlyList<EnrollmentDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<EnrollmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EnrollmentDto> CreateAsync(EnrollmentCreateRequest request, CancellationToken cancellationToken);
    Task<EnrollmentDto> UpdateAsync(Guid id, EnrollmentUpdateRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ILearningProgressService
{
    Task<IReadOnlyList<LearningProgressDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<LearningProgressDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<LearningProgressDto> UpsertAsync(LearningProgressUpsertRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
