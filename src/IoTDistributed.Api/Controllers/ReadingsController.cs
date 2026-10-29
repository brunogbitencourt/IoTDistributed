
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using IoTDistributed.Api.Models;
using IoTDistributed.Api.Persistence;

namespace IoTDistributed.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReadingsController : ControllerBase
{
    private readonly ReadingRepository _repository;

    public ReadingsController(ReadingRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SensorReadingDto>>> GetAll(
        [FromQuery, Range(1, 100)] int limit = 20,
        [FromQuery] string? sensorId = null,
        CancellationToken cancellationToken = default)
    {
        var readings = await _repository.GetAllAsync(
            limit,
            sensorId,
            cancellationToken);

        return Ok(readings);
    }
}
