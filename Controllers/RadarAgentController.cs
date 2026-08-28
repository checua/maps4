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
    public IActionResult Index()
    {
        return View(new RadarAgentPairingPageViewModel
        {
            NombreAgent = "RADAR Agent",
            CuentaNombre = User.FindFirst("CuentaNombre")?.Value
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCodigo(
        RadarAgentPairingPageViewModel model,
        CancellationToken cancellationToken)
    {
        string? correo = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(correo))
            return Forbid();

        RadarAgentPairingCreateResult resultado = await _pairingRepository.CrearCodigoAsync(
            correo,
            model.NombreAgent,
            cancellationToken);

        model.NombreAgent = string.IsNullOrWhiteSpace(model.NombreAgent)
            ? "RADAR Agent"
            : model.NombreAgent.Trim();
        model.CuentaNombre = resultado.CuentaNombre;
        model.Codigo = resultado.Codigo;
        model.ExpiraUtc = resultado.ExpiraUtc;

        return View("Index", model);
    }
}
