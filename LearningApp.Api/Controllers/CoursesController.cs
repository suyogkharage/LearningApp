using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningApp.Api.Controllers;

[ApiController]
[Authorize(Policy = "LearnerOrAdmin")]
[Route("api/[controller]")]
public sealed class CoursesController(ICourseService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CourseDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CourseDto>>.Ok(await service.GetAllAsync(cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CourseDto>>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CourseDto>.Ok(await service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<CourseDto>>> Create(CourseUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.CourseId }, ApiResponse<CourseDto>.Ok(result, "Course created."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<CourseDto>>> Update(Guid id, CourseUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CourseDto>.Ok(await service.UpdateAsync(id, request, cancellationToken), "Course updated."));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, "Course deleted."));
    }
}
