
using Microsoft.AspNetCore.Mvc;
using IoTDistributed.Api.Models;
using IoTDistributed.Api.Persistence;

namespace IoTDistributed.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorsController : ControllerBase
{
    private readonly SensorRepository _repository;

    public SensorsController(SensorRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SensorDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var sensors = await _repository.GetAllAsync(
            cancellationToken);

        return Ok(sensors);
    }
}
