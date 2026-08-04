using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningApp.Api.Controllers;

[ApiController]
[Authorize(Policy = "LearnerOrAdmin")]
[Route("api/[controller]")]
public sealed class EnrollmentsController(IEnrollmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EnrollmentDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<EnrollmentDto>>.Ok(await service.GetAllAsync(cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<EnrollmentDto>>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<EnrollmentDto>.Ok(await service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EnrollmentDto>>> Create(EnrollmentCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.EnrollmentId }, ApiResponse<EnrollmentDto>.Ok(result, "Enrollment created."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<EnrollmentDto>>> Update(Guid id, EnrollmentUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<EnrollmentDto>.Ok(await service.UpdateAsync(id, request, cancellationToken), "Enrollment updated."));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, "Enrollment deleted."));
    }
}
