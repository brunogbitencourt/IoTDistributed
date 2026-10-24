
using Grpc.Core;
using IoTDistributed.Node.Grpc;

namespace IoTDistributed.Node.Services;

public class ClusterGrpcService : Cluster.ClusterBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClusterGrpcService> _logger;

    public ClusterGrpcService(
        IConfiguration configuration,
        ILogger<ClusterGrpcService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override Task<PingReply> Ping(
        PingRequest request,
        ServerCallContext context)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        _logger.LogInformation(
            "NODE {NodeId} recebeu PING do NODE {SenderId}",
            nodeId,
            request.SenderId);

        return Task.FromResult(new PingReply
        {
            ResponderId = nodeId,
            Success = true
        });
    }
}
