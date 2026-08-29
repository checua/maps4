using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[Authorize]
public sealed class RadarAgentDiscoveryController : Controller
{
    private readonly IRadarAgentChatDiscoveryCommandRepository _commandRepository;

    public RadarAgentDiscoveryController(IRadarAgentChatDiscoveryCommandRepository commandRepository)
    {
        _commandRepository = commandRepository;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Solicitar(
        Guid idAgent,
        CancellationToken cancellationToken)
    {
        if (!TryGetContextoActual(out string correo, out int idCuenta))
            return Forbid();

        DateTime? solicitadaUtc = await _commandRepository.SolicitarAsync(
            correo,
            idCuenta,
            idAgent,
            cancellationToken);

        if (!solicitadaUtc.HasValue)
            return NotFound();

        TempData["RadarAgentConfiguracionMensaje"] =
            "Exploración solicitada. El Agent actualizará el catálogo desde WhatsApp en cuanto sincronice la orden; el monitoreo normal no hará barridos automáticos.";

        return RedirectToAction(
            "Configurar",
            "RadarAgent",
            new { idAgent });
    }

    private bool TryGetContextoActual(out string correo, out int idCuenta)
    {
        correo = User.Identity?.Name ?? string.Empty;
        idCuenta = 0;
        string? idCuentaClaim = User.FindFirst("IdCuenta")?.Value;

        return !string.IsNullOrWhiteSpace(correo)
            && int.TryParse(idCuentaClaim, out idCuenta)
            && idCuenta > 0;
    }
}
