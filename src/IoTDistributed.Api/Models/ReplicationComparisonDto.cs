
namespace IoTDistributed.Api.Models;

public sealed record DatabaseScanDto(
    int NodeId,
    string Status,
    int? EventCount
);

public sealed record DivergentEventDto(
    Guid EventId,
    IReadOnlyList<int> MissingInNodes
);

public sealed record ReplicationComparisonDto(
    DateTime FromUtc,
    DateTime ToUtc,
    bool AllDatabasesAvailable,
    IReadOnlyList<DatabaseScanDto> Databases,
    IReadOnlyList<DivergentEventDto> Divergences
);
