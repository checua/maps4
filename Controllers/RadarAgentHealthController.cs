using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[Authorize(Roles = "PROPIETARIO")]
[Route("RadarAgent/Health")]
public sealed class RadarAgentHealthController : Controller
{
    private readonly IRadarAgentHealthRepository _healthRepository;

    public RadarAgentHealthController(IRadarAgentHealthRepository healthRepository)
    {
        _healthRepository = healthRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";

        string correo = User.Identity?.Name ?? string.Empty;
        string? accountClaim = User.FindFirst("IdCuenta")?.Value;
        if (string.IsNullOrWhiteSpace(correo)
            || !int.TryParse(accountClaim, out int idCuenta)
            || idCuenta <= 0)
        {
            return Forbid();
        }

        DateTime serverUtc = DateTime.UtcNow;
        List<RadarAgentHealthRecord> records = await _healthRepository.ListForOwnerAsync(
            correo,
            idCuenta,
            cancellationToken);

        var model = new RadarAgentHealthViewModel
        {
            ServerUtc = serverUtc,
            Agents = records.Select(item =>
            {
                RadarAgentHealthLevel level = RadarAgentHealthPolicy.Evaluate(item, serverUtc);
                bool hasData = item.InstanceId != Guid.Empty;
                return new RadarAgentHealthItemViewModel
                {
                    NombreAgent = item.NombreAgent,
                    Level = level,
                    LevelLabel = RadarAgentHealthPolicy.Label(level),
                    ReceivedUtc = hasData ? item.ReceivedUtc : null,
                    Version = hasData ? item.Version : null,
                    ListenerState = hasData ? item.ListenerState : null,
                    WhatsAppState = hasData ? item.WhatsAppState : null,
                    LastSweepCompletedUtc = hasData ? item.LastSweepCompletedUtc : null,
                    ChatsConfigured = hasData ? item.ChatsConfigured : null,
                    ChatsReviewed = hasData ? item.ChatsReviewed : null,
                    CentralState = hasData ? item.CentralState : null,
                    FallbackEnabled = hasData ? item.FallbackEnabled : null,
                    ErrorCode = hasData ? item.ErrorCode : null
                };
            }).ToList()
        };

        return View(model);
    }
}
