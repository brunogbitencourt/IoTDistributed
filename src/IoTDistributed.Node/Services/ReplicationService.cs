
using Grpc.Core;
using Grpc.Net.Client;
using IoTDistributed.Node.Grpc;
using IoTDistributed.Node.Sensors;

namespace IoTDistributed.Node.Services;

public class ReplicationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicationService> _logger;

    public ReplicationService(
        IConfiguration configuration,
        ILogger<ReplicationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ReplicateAsync(
        SensorReading reading,
        CancellationToken cancellationToken = default)
    {
        string peersConfig = _configuration["PEERS"] ?? "";

        string[] peers = peersConfig.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        var tasks = peers.Select(peer =>
            ReplicateToPeerAsync(
                peer,
                reading,
                cancellationToken));

        bool[] results = await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Replicação do evento {EventId}: {Success}/{Total} peers confirmaram",
            reading.EventId,
            results.Count(success => success),
            results.Length);
    }

    private async Task<bool> ReplicateToPeerAsync(
        string peer,
        SensorReading reading,
        CancellationToken cancellationToken)
    {
        string[] parts = peer.Split('=', 2);

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out int peerId) ||
            string.IsNullOrWhiteSpace(parts[1]))
        {
            _logger.LogError(
                "Configuração de peer inválida: {Peer}",
                peer);

            return false;
        }

        try
        {
            using var channel = GrpcChannel.ForAddress(parts[1]);

            var client = new Cluster.ClusterClient(channel);

            var request = new ReplicateReadingRequest
            {
                EventId = reading.EventId.ToString(),
                SensorId = reading.SensorId,
                Value = reading.Value,
                CollectedAtUnixMs =
                    new DateTimeOffset(reading.CollectedAtUtc)
                        .ToUnixTimeMilliseconds(),
                OriginNodeId = reading.OriginNodeId
            };

            var response = await client.ReplicateReadingAsync(
                request,
                deadline: DateTime.UtcNow.AddSeconds(3),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Evento {EventId} confirmado pelo NODE {NodeId}. Nova inserção: {Inserted}",
                reading.EventId,
                response.NodeId,
                response.Inserted);

            return true;
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(
                "Falha ao replicar evento {EventId} para NODE {PeerId}: {Status}",
                reading.EventId,
                peerId,
                ex.StatusCode);

            return false;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao replicar evento {EventId} para NODE {PeerId}",
                reading.EventId,
                peerId);

            return false;
        }
    }
}
