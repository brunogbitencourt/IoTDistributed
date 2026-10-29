
using Dapper;
using IoTDistributed.Api.Database;
using IoTDistributed.Api.Models;
using Npgsql;

namespace IoTDistributed.Api.Persistence;

public class ReadingRepository
{

    private readonly ClusterDataSources _dataSources;

    public ReadingRepository(ClusterDataSources dataSources)
    {
        _dataSources = dataSources;
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
}
