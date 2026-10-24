
using Dapper;
using Npgsql;
using IoTDistributed.Node.Sensors;

namespace IoTDistributed.Node.Persistence;

public class SensorRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public SensorRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> SaveAsync(
        SensorReading reading,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO sensor_readings
            (
                event_id,
                sensor_id,
                value,
                collected_at,
                origin_node_id
            )
            VALUES
            (
                @EventId,
                @SensorId,
                @Value,
                @CollectedAtUtc,
                @OriginNodeId
            )
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: reading,
            cancellationToken: cancellationToken
        );

        int affectedRows = await connection.ExecuteAsync(command);

        return affectedRows == 1;
    }



    public async Task<int?> GetOwnerNodeIdAsync(
        string sensorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT owner_node_id
        FROM sensors
        WHERE sensor_id = @SensorId;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SensorId = sensorId },
            cancellationToken: cancellationToken
        );

        return await connection.QuerySingleOrDefaultAsync<int?>(command);
    }



    public async Task<IReadOnlyList<string>> GetMqttTopicsByNodeAsync(
        int nodeId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT DISTINCT mqtt_topic
        FROM sensors
        WHERE owner_node_id = @NodeId
          AND mqtt_topic IS NOT NULL
        ORDER BY mqtt_topic;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { NodeId = nodeId },
            cancellationToken: cancellationToken
        );

        var topics = await connection.QueryAsync<string>(command);

        return topics.ToArray();
    }


    public async Task<string?> GetMqttTopicAsync(
        string sensorId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT mqtt_topic
        FROM sensors
        WHERE sensor_id = @SensorId;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SensorId = sensorId },
            cancellationToken: cancellationToken
        );

        return await connection.QuerySingleOrDefaultAsync<string>(
            command);
    }




}
