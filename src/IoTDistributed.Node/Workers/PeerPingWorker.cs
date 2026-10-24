
using Grpc.Core;
using Grpc.Net.Client;
using IoTDistributed.Node.Grpc;

namespace IoTDistributed.Node.Workers;

public class PeerPingWorker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PeerPingWorker> _logger;

    public PeerPingWorker(
        IConfiguration configuration,
        ILogger<PeerPingWorker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        string[] peers = (_configuration["PEERS"] ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (string peer in peers)
            {
                string[] parts = peer.Split('=', 2);

                if (parts.Length != 2)
                    continue;

                try
                {
                    using var channel =
                        GrpcChannel.ForAddress(parts[1]);

                    var client = new Cluster.ClusterClient(channel);

                    var response = await client.PingAsync(
                        new PingRequest { SenderId = nodeId },
                        deadline: DateTime.UtcNow.AddSeconds(3),
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "NODE {NodeId} recebeu resposta do NODE {PeerId}: {Success}",
                        nodeId,
                        response.ResponderId,
                        response.Success);
                }
                catch (RpcException ex)
                {
                    _logger.LogWarning(
                        "NODE {NodeId}: falha ao comunicar com {Peer}: {Status}",
                        nodeId,
                        peer,
                        ex.StatusCode);
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }
}
