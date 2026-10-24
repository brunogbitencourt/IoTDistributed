
using Grpc.Core;
using IoTDistributed.Node.Grpc;
using IoTDistributed.Node.Persistence;
using IoTDistributed.Node.Sensors;


namespace IoTDistributed.Node.Services;

public class ClusterGrpcService : Cluster.ClusterBase
{

    private readonly ILogger<ClusterGrpcService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SensorRepository _repository;

    public ClusterGrpcService(
        ILogger<ClusterGrpcService> logger,
        IConfiguration configuration,
        SensorRepository repository)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;
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



    public override async Task<SensorReadingReply> SubmitReading(
        SensorReadingRequest request,
        ServerCallContext context)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        _logger.LogWarning(
    "Mensagem gRPC: {MessageSize} bytes. Conteúdo: {Request}",
    request.CalculateSize(),
    request.ToString());

        if (nodeId <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.Internal,
                "NODE_ID não configurado."));
        }

        _logger.LogWarning(
    "SubmitReading recebido: EventId='{EventId}', Tamanho={Length}, SensorId='{SensorId}'",
    request.EventId,
    request.EventId.Length,
    request.SensorId
);

        if (!Guid.TryParse(request.EventId, out Guid eventId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "EventId inválido."));
        }

        if (string.IsNullOrWhiteSpace(request.SensorId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "SensorId é obrigatório."));
        }

        if (!double.IsFinite(request.Value))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Valor de medição inválido."));
        }

        DateTime collectedAtUtc;

        try
        {
            collectedAtUtc = DateTimeOffset
                .FromUnixTimeMilliseconds(request.CollectedAtUnixMs)
                .UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Data da medição inválida."));
        }

        int? ownerNodeId = await _repository.GetOwnerNodeIdAsync(
            request.SensorId,
            context.CancellationToken);

        if (ownerNodeId is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                "Sensor não cadastrado."));
        }

        if (ownerNodeId != nodeId)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                $"Sensor pertence ao nó {ownerNodeId}."));
        }

        var reading = new SensorReading(
            EventId: eventId,
            SensorId: request.SensorId,
            Value: request.Value,
            CollectedAtUtc: collectedAtUtc,
            OriginNodeId: nodeId
        );

        bool inserted = await _repository.SaveAsync(
            reading,
            context.CancellationToken);

        _logger.LogWarning(
    "SubmitReading recebido: EventId='{EventId}', Tamanho={Length}, SensorId='{SensorId}'",
    request.EventId,
    request.EventId.Length,
    request.SensorId
);
        return new SensorReadingReply
        {
            EventId = eventId.ToString(),
            Inserted = inserted,
            NodeId = nodeId
        };
    }


}
