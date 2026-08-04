using Microsoft.AspNetCore.Mvc;

namespace LearningApp.UI.Server.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get() => Ok(new { status = "Healthy", utc = DateTime.UtcNow });
}
