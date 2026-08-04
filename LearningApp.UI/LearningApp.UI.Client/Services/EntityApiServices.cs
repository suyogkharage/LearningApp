using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;

namespace LearningApp.UI.Client.Services;

public sealed class CategoryApiService(ApiClient api)
{
    public Task<IReadOnlyList<CategoryDto>> GetAllAsync() => ReadList(api.GetAsync<IReadOnlyList<CategoryDto>>("api/categories"));
    public Task SaveAsync(Guid? id, CategoryUpsertRequest request) => id is null ? Ensure(api.PostAsync<CategoryDto>("api/categories", request)) : Ensure(api.PutAsync<CategoryDto>($"api/categories/{id}", request));
    public Task DeleteAsync(Guid id) => Ensure(api.DeleteAsync($"api/categories/{id}"));
    private static async Task<IReadOnlyList<T>> ReadList<T>(Task<ApiResponse<IReadOnlyList<T>>> task)
    {
        var response = await task;
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message);
        }

        return response.Data ?? Array.Empty<T>();
    }
    private static async Task Ensure<T>(Task<ApiResponse<T>> task) { var response = await task; if (!response.Success) throw new InvalidOperationException(response.Message); }
}

public sealed class CourseApiService(ApiClient api)
{
    public Task<IReadOnlyList<CourseDto>> GetAllAsync() => ReadList(api.GetAsync<IReadOnlyList<CourseDto>>("api/courses"));
    public Task SaveAsync(Guid? id, CourseUpsertRequest request) => id is null ? Ensure(api.PostAsync<CourseDto>("api/courses", request)) : Ensure(api.PutAsync<CourseDto>($"api/courses/{id}", request));
    public Task DeleteAsync(Guid id) => Ensure(api.DeleteAsync($"api/courses/{id}"));
    private static async Task<IReadOnlyList<T>> ReadList<T>(Task<ApiResponse<IReadOnlyList<T>>> task)
    {
        var response = await task;
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message);
        }

        return response.Data ?? Array.Empty<T>();
    }
    private static async Task Ensure<T>(Task<ApiResponse<T>> task) { var response = await task; if (!response.Success) throw new InvalidOperationException(response.Message); }
}

public sealed class UserApiService(ApiClient api)
{
    public Task<IReadOnlyList<UserDto>> GetAllAsync() => ReadList(api.GetAsync<IReadOnlyList<UserDto>>("api/users"));
    public Task CreateAsync(UserCreateRequest request) => Ensure(api.PostAsync<UserDto>("api/users", request));
    public Task UpdateAsync(Guid id, UserUpdateRequest request) => Ensure(api.PutAsync<UserDto>($"api/users/{id}", request));
    public Task DeleteAsync(Guid id) => Ensure(api.DeleteAsync($"api/users/{id}"));
    private static async Task<IReadOnlyList<T>> ReadList<T>(Task<ApiResponse<IReadOnlyList<T>>> task)
    {
        var response = await task;
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message);
        }

        return response.Data ?? Array.Empty<T>();
    }
    private static async Task Ensure<T>(Task<ApiResponse<T>> task) { var response = await task; if (!response.Success) throw new InvalidOperationException(response.Message); }
}

public sealed class EnrollmentApiService(ApiClient api)
{
    public Task<IReadOnlyList<EnrollmentDto>> GetAllAsync() => ReadList(api.GetAsync<IReadOnlyList<EnrollmentDto>>("api/enrollments"));
    public Task CreateAsync(EnrollmentCreateRequest request) => Ensure(api.PostAsync<EnrollmentDto>("api/enrollments", request));
    public Task UpdateAsync(Guid id, EnrollmentUpdateRequest request) => Ensure(api.PutAsync<EnrollmentDto>($"api/enrollments/{id}", request));
    public Task DeleteAsync(Guid id) => Ensure(api.DeleteAsync($"api/enrollments/{id}"));
    private static async Task<IReadOnlyList<T>> ReadList<T>(Task<ApiResponse<IReadOnlyList<T>>> task)
    {
        var response = await task;
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message);
        }

        return response.Data ?? Array.Empty<T>();
    }
    private static async Task Ensure<T>(Task<ApiResponse<T>> task) { var response = await task; if (!response.Success) throw new InvalidOperationException(response.Message); }
}

public sealed class LearningProgressApiService(ApiClient api)
{
    public Task<IReadOnlyList<LearningProgressDto>> GetAllAsync() => ReadList(api.GetAsync<IReadOnlyList<LearningProgressDto>>("api/learningprogress"));
    public Task SaveAsync(LearningProgressUpsertRequest request) => Ensure(api.PostAsync<LearningProgressDto>("api/learningprogress", request));
    public Task DeleteAsync(Guid id) => Ensure(api.DeleteAsync($"api/learningprogress/{id}"));
    private static async Task<IReadOnlyList<T>> ReadList<T>(Task<ApiResponse<IReadOnlyList<T>>> task)
    {
        var response = await task;
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message);
        }

        return response.Data ?? Array.Empty<T>();
    }
    private static async Task Ensure<T>(Task<ApiResponse<T>> task) { var response = await task; if (!response.Success) throw new InvalidOperationException(response.Message); }
}
