
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
}
