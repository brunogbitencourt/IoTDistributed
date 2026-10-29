
namespace IoTDistributed.Api.Models;

public sealed record ReplicaStatusDto(
    int NodeId,
    string Status
);

public sealed record ReadingReplicasDto(
    Guid EventId,
    IReadOnlyList<ReplicaStatusDto> Replicas
);
