
using Npgsql;

namespace IoTDistributed.Api.Database;

public sealed class ClusterDataSources : IAsyncDisposable
{
    private readonly Dictionary<int, NpgsqlDataSource> _sources = new();

    public ClusterDataSources(IConfiguration configuration)
    {
        try
        {
            for (int nodeId = 1; nodeId <= 3; nodeId++)
            {
                string connectionName = $"Db{nodeId}";

                string connectionString =
                    configuration.GetConnectionString(connectionName)
                    ?? throw new InvalidOperationException(
                        $"Conexão {connectionName} não configurada.");
                
                _sources.Add(
                    nodeId,
                    NpgsqlDataSource.Create(connectionString));
            }
        }
        catch
        {
            foreach (var source in _sources.Values)
                source.Dispose();

            throw;
        }
    }

    public NpgsqlDataSource Get(int nodeId)
    {
        if (!_sources.TryGetValue(nodeId, out var source))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nodeId),
                $"Banco do NODE {nodeId} não configurado.");
        }

        return source;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values)
        {
            await source.DisposeAsync();
        }
    }
}
