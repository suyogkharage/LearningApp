using LearningApp.UI.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace LearningApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status410Gone)]
    public ActionResult<ApiErrorResponse> Login() =>
        StatusCode(
            StatusCodes.Status410Gone,
            ApiErrorResponse.Create(
                StatusCodes.Status410Gone,
                "Local API-issued JWT login is disabled. Use Microsoft account sign-in through the Blazor UI.",
                traceId: HttpContext.TraceIdentifier,
                correlationId: HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()));
}
