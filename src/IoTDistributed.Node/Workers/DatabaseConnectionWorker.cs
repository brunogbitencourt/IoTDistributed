
using Npgsql;

namespace IoTDistributed.Node.Workers;

public class DatabaseConnectionWorker : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseConnectionWorker> _logger;

    public DatabaseConnectionWorker(
        NpgsqlDataSource dataSource,
        IConfiguration configuration,
        ILogger<DatabaseConnectionWorker> logger)
    {
        _dataSource = dataSource;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(stoppingToken);

            await using var command = new NpgsqlCommand(
                "SELECT current_database(), inet_server_addr()::text",
                connection);

            await using var reader =
                await command.ExecuteReaderAsync(stoppingToken);

            if (await reader.ReadAsync(stoppingToken))
            {
                string database = reader.GetString(0);
                string serverAddress = reader.GetString(1);

                _logger.LogInformation(
                    "NODE {NodeId} conectado ao banco {Database}, servidor {Address}",
                    nodeId,
                    database,
                    serverAddress);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do serviço.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "NODE {NodeId}: erro ao conectar ao PostgreSQL",
                nodeId);
        }
    }
}
