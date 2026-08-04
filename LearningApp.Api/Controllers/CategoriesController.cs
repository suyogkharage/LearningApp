using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningApp.Api.Controllers;

[ApiController]
[Authorize(Policy = "LearnerOrAdmin")]
[Route("api/[controller]")]
public sealed class CategoriesController(ICategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(await service.GetAllAsync(cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CategoryDto>.Ok(await service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(CategoryUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.CategoryId }, ApiResponse<CategoryDto>.Ok(result, "Category created."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(Guid id, CategoryUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CategoryDto>.Ok(await service.UpdateAsync(id, request, cancellationToken), "Category updated."));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, "Category deleted."));
    }
}
