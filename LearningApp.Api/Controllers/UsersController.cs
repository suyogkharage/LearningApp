using LearningApp.Api.Services;
using LearningApp.UI.Shared.Common;
using LearningApp.UI.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearningApp.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/[controller]")]
public sealed class UsersController(IUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<UserDto>>.Ok(await service.GetAllAsync(cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<UserDto>.Ok(await service.GetByIdAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(UserCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.UserId }, ApiResponse<UserDto>.Ok(result, "User created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, UserUpdateRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<UserDto>.Ok(await service.UpdateAsync(id, request, cancellationToken), "User updated."));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, "User deleted."));
    }
}
