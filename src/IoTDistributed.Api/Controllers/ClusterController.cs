
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Npgsql;
using IoTDistributed.Api.Database;

namespace IoTDistributed.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClusterController : ControllerBase
{
    private readonly ClusterDataSources _dataSources;
    private readonly ILogger<ClusterController> _logger;

    public ClusterController(
    ClusterDataSources dataSources,
    ILogger<ClusterController> logger)
    {
        _dataSources = dataSources;
        _logger = logger;
    }



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


    [HttpGet("databases")]
    public async Task<IActionResult> GetDatabases(
        CancellationToken cancellationToken)
    {
        var results = new List<object>();

        for (int nodeId = 1; nodeId <= 3; nodeId++)
        {
            try
            {
                var dataSource = _dataSources.Get(nodeId);

                await using var connection =
                    await dataSource.OpenConnectionAsync(
                        cancellationToken);

                var command = new CommandDefinition(
                    commandText: "SELECT COUNT(*) FROM sensors;",
                    cancellationToken: cancellationToken);

                int sensorCount =
                    await connection.ExecuteScalarAsync<int>(command);

                results.Add(new
                {
                    NodeId = nodeId,
                    DatabaseStatus = "online",
                    SensorCount = (int?)sensorCount
                });
            }
            catch (Exception ex)
                when (ex is NpgsqlException or TimeoutException)
            {
                _logger.LogWarning(
                    ex,
                    "Falha ao consultar o banco do NODE {NodeId}",
                    nodeId);

                results.Add(new
                {
                    NodeId = nodeId,
                    DatabaseStatus = "offline",
                    SensorCount = (int?)null
                });
            }
        }

        return Ok(results);
    }



}
