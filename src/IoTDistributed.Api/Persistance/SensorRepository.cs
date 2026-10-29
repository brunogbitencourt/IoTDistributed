
using Dapper;
using Npgsql;
using IoTDistributed.Api.Models;

namespace IoTDistributed.Api.Persistence;

public class SensorRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public SensorRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<SensorDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                sensor_id AS "SensorId",
                sensor_type AS "SensorType",
                unit AS "Unit",
                owner_node_id AS "OwnerNodeId",
                mqtt_topic AS "MqttTopic"
            FROM sensors
            ORDER BY sensor_id;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            commandText: sql,
            cancellationToken: cancellationToken
        );

        var sensors =
            await connection.QueryAsync<SensorDto>(command);

        return sensors.ToArray();
    }
}
