
using IoTDistributed.Api.Models;
using IoTDistributed.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IoTDistributed.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReplicationController : ControllerBase
{
    private readonly ReplicationComparisonService _comparisonService;

    public ReplicationController(
        ReplicationComparisonService comparisonService)
    {
        _comparisonService = comparisonService;
    }

    [HttpGet("divergences")]
    public async Task<ActionResult<ReplicationComparisonDto>> GetDivergences(
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        // Define um único horário de referência.
        var now = DateTimeOffset.UtcNow;

        // Ambos os parâmetros devem ser informados juntos.
        if (fromUtc.HasValue != toUtc.HasValue)
        {
            return BadRequest(
                "Informe fromUtc e toUtc juntos.");
        }

        // Janela padrão: últimos 15 minutos.
        var from = fromUtc ?? now.AddMinutes(-15);
        var to = toUtc ?? now;

        // Valida o intervalo.
        if (from >= to)
        {
            return BadRequest(
                "fromUtc deve ser anterior a toUtc.");
        }

        // Evita janelas excessivamente grandes nesta versão.
        if (to - from > TimeSpan.FromHours(1))
        {
            return BadRequest(
                "O intervalo máximo permitido é de 1 hora.");
        }

        var result = await _comparisonService.CompareAsync(
            from.UtcDateTime,
            to.UtcDateTime,
            cancellationToken);

        return Ok(result);
    }
}
