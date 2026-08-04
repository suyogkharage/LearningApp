using Microsoft.AspNetCore.Mvc;

namespace LearningApp.UI.Server.Controllers;

[ApiController]
[Route("configuration")]
public sealed class ConfigurationController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("api-base-url")]
    public ActionResult<object> GetApiBaseUrl() => Ok(new { apiBaseUrl = configuration["Api:BaseUrl"] });
}
