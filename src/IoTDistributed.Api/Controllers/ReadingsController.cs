
using IoTDistributed.Api.Models;
using IoTDistributed.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

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
        [FromQuery, Range(1, 3)] int node = 1,
        [FromQuery, Range(1, 100)] int limit = 20,
        [FromQuery] string? sensorId = null,
        CancellationToken cancellationToken = default)
    {
        var readings = await _repository.GetAllAsync(
            node,
            limit,
            sensorId,
            cancellationToken);

        return Ok(readings);
    }


    [HttpGet("{eventId:guid}/replicas")]
    public async Task<ActionResult<ReadingReplicasDto>> GetReplicas(
        [FromRoute] Guid eventId,
        CancellationToken cancellationToken)
    {
        var result = await _repository.GetReplicasAsync(
            eventId,
            cancellationToken);

        return Ok(result);
    }



}
