
using Dapper;
using IoTDistributed.Api.Database;
using IoTDistributed.Api.Models;
using Npgsql;

namespace IoTDistributed.Api.Persistence;

public class ReadingRepository
{


    private readonly ClusterDataSources _dataSources;
    private readonly ILogger<ReadingRepository> _logger;

    public ReadingRepository(
        ClusterDataSources dataSources,
        ILogger<ReadingRepository> logger)
    {
        _dataSources = dataSources;
        _logger = logger;
    }


    public async Task<IReadOnlyList<SensorReadingDto>> GetAllAsync(
        int nodeId,
        int limit,
        string? sensorId,
        CancellationToken cancellationToken = default)
    {
        string sql = """
            SELECT
                event_id AS "EventId",
                sensor_id AS "SensorId",
                value AS "Value",
                collected_at AS "CollectedAtUtc",
                origin_node_id AS "OriginNodeId",
                created_at AS "CreatedAtUtc"
            FROM sensor_readings
            """;

        // Acrescenta o filtro somente quando informado.
        if (!string.IsNullOrWhiteSpace(sensorId))
        {
            sql += "\nWHERE sensor_id = @SensorId";
        }

        sql += """
            
            ORDER BY created_at DESC, event_id DESC
            LIMIT @Limit;
            """;


        var dataSource = _dataSources.Get(nodeId);

        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);


        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                SensorId = sensorId,
                Limit = limit
            },
            cancellationToken: cancellationToken
        );

        var readings =
            await connection.QueryAsync<SensorReadingDto>(command);

        return readings.ToArray();
    }


    public async Task<ReadingReplicasDto> GetReplicasAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT EXISTS (
            SELECT 1
            FROM sensor_readings
            WHERE event_id = @EventId
        );
        """;

        var replicas = new List<ReplicaStatusDto>();

        for (int nodeId = 1; nodeId <= 3; nodeId++)
        {
            try
            {
                var dataSource = _dataSources.Get(nodeId);

                await using var connection =
                    await dataSource.OpenConnectionAsync(
                        cancellationToken);

                var command = new CommandDefinition(
                    commandText: sql,
                    parameters: new { EventId = eventId },
                    cancellationToken: cancellationToken
                );

                bool exists =
                    await connection.ExecuteScalarAsync<bool>(
                        command);

                string status = exists
                    ? "present"
                    : "missing";

                replicas.Add(
                    new ReplicaStatusDto(nodeId, status));
            }
            catch (Exception ex) when (
                ex is TimeoutException ||
                (ex is NpgsqlException && ex is not PostgresException))
            {
                _logger.LogWarning(
                    ex,
                    "Falha ao consultar o DB{NodeId} para o evento {EventId}",
                    nodeId,
                    eventId);

                replicas.Add(
                    new ReplicaStatusDto(
                        nodeId,
                        "unavailable"));
            }
        }

        return new ReadingReplicasDto(
            eventId,
            replicas);
    }



}
