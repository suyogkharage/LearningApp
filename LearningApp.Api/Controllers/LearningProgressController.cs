using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningApp.Api.Controllers;

[ApiController]
[Authorize(Policy = "LearnerOrAdmin")]
[Route("api/[controller]")]
public sealed class LearningProgressController(ILearningProgressService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LearningProgressDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<LearningProgressDto>>.Ok(await service.GetAllAsync(cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LearningProgressDto>>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LearningProgressDto>.Ok(await service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<LearningProgressDto>>> Upsert(LearningProgressUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LearningProgressDto>.Ok(await service.UpsertAsync(request, cancellationToken), "Learning progress saved."));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, "Learning progress deleted."));
    }
}
