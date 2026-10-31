
using IoTDistributed.Api.Models;
using IoTDistributed.Api.Persistence;
using Npgsql;

namespace IoTDistributed.Api.Services;

public sealed class ReplicationComparisonService
{
    private readonly ReadingRepository _repository;
    private readonly ILogger<ReplicationComparisonService> _logger;

    public ReplicationComparisonService(
        ReadingRepository repository,
        ILogger<ReplicationComparisonService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ReplicationComparisonDto> CompareAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        // 1. Armazena os conjuntos recuperados de cada banco.
        var eventsByNode = new Dictionary<int, HashSet<Guid>>();
        var databases = new List<DatabaseScanDto>();

        // 2. Consulta os três PostgreSQL.
        for (int nodeId = 1; nodeId <= 3; nodeId++)
        {
            try
            {
                var eventIds =
                    await _repository.GetEventIdsInWindowAsync(
                        nodeId,
                        fromUtc,
                        toUtc,
                        cancellationToken);

                var eventSet = eventIds.ToHashSet();

                eventsByNode.Add(nodeId, eventSet);

                databases.Add(new DatabaseScanDto(
                    nodeId,
                    "online",
                    eventSet.Count));
            }
            catch (Exception ex) when (
                ex is TimeoutException ||
                (ex is NpgsqlException && ex is not PostgresException))
            {
                _logger.LogWarning(
                    ex,
                    "Falha ao consultar eventos do DB{NodeId}",
                    nodeId);

                databases.Add(new DatabaseScanDto(
                    nodeId,
                    "unavailable",
                    null));
            }
        }

        // 3. Calcula a união dos eventos conhecidos.
        var union = new HashSet<Guid>();

        foreach (var eventSet in eventsByNode.Values)
        {
            union.UnionWith(eventSet);
        }

        // 4. Descobre quais eventos faltam em cada banco acessível.
        var missingByEvent = new Dictionary<Guid, List<int>>();

        foreach (var (nodeId, eventSet) in eventsByNode)
        {
            var missing = new HashSet<Guid>(union);

            missing.ExceptWith(eventSet);

            foreach (var eventId in missing)
            {
                if (!missingByEvent.TryGetValue(
                    eventId, out var missingNodes))
                {
                    missingNodes = new List<int>();
                    missingByEvent.Add(eventId, missingNodes);
                }

                missingNodes.Add(nodeId);
            }
        }

        // 5. Organiza o resultado.
        var divergences = missingByEvent
            .OrderBy(item => item.Key)
            .Select(item => new DivergentEventDto(
                item.Key,
                item.Value.OrderBy(id => id).ToArray()))
            .ToArray();

        bool allDatabasesAvailable =
            databases.All(db => db.Status == "online");

        return new ReplicationComparisonDto(
            fromUtc,
            toUtc,
            allDatabasesAvailable,
            databases,
            divergences);
    }
}
