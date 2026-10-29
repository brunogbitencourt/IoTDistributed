
using Dapper;
using IoTDistributed.Api.Database;
using IoTDistributed.Api.Models;
using Npgsql;

namespace IoTDistributed.Api.Persistence;

public class SensorRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public SensorRepository(ClusterDataSources dataSources)
    {
        _dataSource = dataSources.Get(1);
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
