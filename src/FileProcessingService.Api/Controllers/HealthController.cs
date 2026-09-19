using Microsoft.AspNetCore.Mvc;

namespace FileProcessingService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new { status = "Healthy", timestampUtc = DateTimeOffset.UtcNow });
    }
}
