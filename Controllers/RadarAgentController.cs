using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[Authorize]
public sealed class RadarAgentController : Controller
{
    private readonly IRadarAgentPairingRepository _pairingRepository;

    public RadarAgentController(IRadarAgentPairingRepository pairingRepository)
    {
        _pairingRepository = pairingRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetContextoActual(out string correo, out int idCuenta))
            return Forbid();

        var model = new RadarAgentPairingPageViewModel
        {
            NombreAgent = "RADAR Agent",
            CuentaNombre = User.FindFirst("CuentaNombre")?.Value,
            Agents = await _pairingRepository.ListarAgentsAsync(correo, idCuenta, cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCodigo(
        RadarAgentPairingPageViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetContextoActual(out string correo, out int idCuenta))
            return Forbid();

        RadarAgentPairingCreateResult resultado = await _pairingRepository.CrearCodigoAsync(
            correo,
            idCuenta,
            model.NombreAgent,
            cancellationToken);

        model.NombreAgent = string.IsNullOrWhiteSpace(model.NombreAgent)
            ? "RADAR Agent"
            : model.NombreAgent.Trim();
        model.CuentaNombre = resultado.CuentaNombre;
        model.Codigo = resultado.Codigo;
        model.ExpiraUtc = resultado.ExpiraUtc;
        model.Agents = await _pairingRepository.ListarAgentsAsync(correo, idCuenta, cancellationToken);

        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revocar(
        Guid idAgent,
        CancellationToken cancellationToken)
    {
        if (!TryGetContextoActual(out string correo, out int idCuenta))
            return Forbid();

        bool revocado = await _pairingRepository.RevocarAgentAsync(
            correo,
            idCuenta,
            idAgent,
            cancellationToken);

        if (!revocado)
            return NotFound();

        TempData["RadarAgentMensaje"] = "El acceso del RADAR Agent fue revocado.";
        return RedirectToAction(nameof(Index));
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
