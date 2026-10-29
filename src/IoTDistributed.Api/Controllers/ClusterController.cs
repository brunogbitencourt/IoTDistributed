
using Microsoft.AspNetCore.Mvc;

namespace IoTDistributed.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClusterController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Service = "IoTDistributed.Api",
            Status = "Healthy",
            TimestampUtc = DateTime.UtcNow
        });
    }
}
