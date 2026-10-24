
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
    private readonly ReplicationService _replicationService;
    private readonly SensorIngestionService _ingestionService;


    public ClusterGrpcService(
        ILogger<ClusterGrpcService> logger,
        IConfiguration configuration,
        SensorRepository repository,
        SensorIngestionService ingestionService)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;
        _ingestionService = ingestionService;
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

        if (!Guid.TryParse(request.EventId, out Guid eventId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "EventId inválido."));
        }

        if (string.IsNullOrWhiteSpace(request.SensorId) ||
            !double.IsFinite(request.Value))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Dados da medição inválidos."));
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

        var reading = new SensorReading(
            EventId: eventId,
            SensorId: request.SensorId,
            Value: request.Value,
            CollectedAtUtc: collectedAtUtc,
            OriginNodeId: nodeId
        );

        try
        {
            bool inserted = await _ingestionService.ProcessAsync(
                reading,
                cancellationToken: context.CancellationToken);

            return new SensorReadingReply
            {
                EventId = eventId.ToString(),
                Inserted = inserted,
                NodeId = nodeId
            };
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                ex.Message));
        }
    }



    public override async Task<ReplicateReadingReply> ReplicateReading(
        ReplicateReadingRequest request,
        ServerCallContext context)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        if (!Guid.TryParse(request.EventId, out Guid eventId))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "EventId inválido."));
        }

        if (string.IsNullOrWhiteSpace(request.SensorId) ||
            !double.IsFinite(request.Value) ||
            request.OriginNodeId <= 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "Dados da réplica inválidos."));
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

        if (ownerNodeId != request.OriginNodeId)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                "Origem da medição não corresponde ao sensor."));
        }

        var reading = new SensorReading(
            EventId: eventId,
            SensorId: request.SensorId,
            Value: request.Value,
            CollectedAtUtc: collectedAtUtc,
            OriginNodeId: request.OriginNodeId
        );

        bool inserted = await _repository.SaveAsync(
            reading,
            context.CancellationToken);

        _logger.LogInformation(
            "NODE {NodeId}: réplica {EventId} recebida do NODE {Origin}. Inserida: {Inserted}",
            nodeId,
            eventId,
            request.OriginNodeId,
            inserted);

        return new ReplicateReadingReply
        {
            EventId = eventId.ToString(),
            Inserted = inserted,
            NodeId = nodeId
        };
    }



}
