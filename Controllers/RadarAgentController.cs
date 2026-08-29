using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace maps4.Controllers;

[Authorize]
public sealed class RadarAgentController : Controller
{
    private static readonly HashSet<int> IntervalosPermitidos =
    [
        10_000,
        30_000,
        60_000,
        300_000,
        1_200_000
    ];

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

    [HttpGet]
    public async Task<IActionResult> Configurar(
        Guid idAgent,
        CancellationToken cancellationToken)
    {
        if (!TryGetContextoActual(out string correo, out int idCuenta))
            return Forbid();

        RadarAgentConfiguration? configuracion = await _pairingRepository.ObtenerConfiguracionAsync(
            correo,
            idCuenta,
            idAgent,
            cancellationToken);

        if (configuracion is null)
            return NotFound();

        var model = new RadarAgentConfigurationEditViewModel
        {
            IdAgent = configuracion.IdAgent,
            NombreAgent = configuracion.NombreAgent,
            EquipoNombre = configuracion.EquipoNombre,
            Activo = configuracion.Activo && !configuracion.RevocadoUtc.HasValue,
            Configurada = configuracion.Configurada,
            ChatsTexto = string.Join(Environment.NewLine, configuracion.ChatsMonitoreados),
            DestinoAlertas = string.IsNullOrWhiteSpace(configuracion.DestinoAlertas)
                ? "Propiedades"
                : configuracion.DestinoAlertas,
            IntervaloRevisionMs = configuracion.IntervaloRevisionMs,
            ActualizadoUtc = configuracion.ActualizadoUtc
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarConfiguracion(
        RadarAgentConfigurationEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetContextoActual(out string correo, out int idCuenta))
            return Forbid();

        if (model.IdAgent == Guid.Empty)
            return BadRequest();

        if (!IntervalosPermitidos.Contains(model.IntervaloRevisionMs))
            ModelState.AddModelError(nameof(model.IntervaloRevisionMs), "Selecciona un intervalo permitido.");

        var chats = (model.ChatsTexto ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        if (chats.Count == 0)
            ModelState.AddModelError(nameof(model.ChatsTexto), "Agrega al menos un chat para monitorear.");

        if (!ModelState.IsValid)
        {
            RadarAgentConfiguration? actual = await _pairingRepository.ObtenerConfiguracionAsync(
                correo,
                idCuenta,
                model.IdAgent,
                cancellationToken);
            if (actual is null)
                return NotFound();

            model.NombreAgent = actual.NombreAgent;
            model.EquipoNombre = actual.EquipoNombre;
            model.Activo = actual.Activo && !actual.RevocadoUtc.HasValue;
            model.Configurada = actual.Configurada;
            model.ActualizadoUtc = actual.ActualizadoUtc;
            return View("Configurar", model);
        }

        RadarAgentConfiguration? existente = await _pairingRepository.ObtenerConfiguracionAsync(
            correo,
            idCuenta,
            model.IdAgent,
            cancellationToken);

        if (existente is null)
            return NotFound();

        var configuracion = new RadarAgentConfiguration
        {
            IdAgent = model.IdAgent,
            ChatsMonitoreados = chats,
            DestinoAlertas = string.IsNullOrWhiteSpace(model.DestinoAlertas)
                ? "Propiedades"
                : model.DestinoAlertas.Trim(),
            IntervaloRevisionMs = model.IntervaloRevisionMs,
            TerminosBusqueda = existente.TerminosBusqueda
        };

        bool guardada = await _pairingRepository.GuardarConfiguracionAsync(
            correo,
            idCuenta,
            configuracion,
            cancellationToken);

        if (!guardada)
            return Forbid();

        TempData["RadarAgentConfiguracionMensaje"] =
            "Configuración guardada en RSMaps. El Agent la sincronizará automáticamente.";

        return RedirectToAction(nameof(Configurar), new { idAgent = model.IdAgent });
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
