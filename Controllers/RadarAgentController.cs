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
    private readonly IRadarAgentChatDiscoveryRepository _chatDiscoveryRepository;

    public RadarAgentController(
        IRadarAgentPairingRepository pairingRepository,
        IRadarAgentChatDiscoveryRepository chatDiscoveryRepository)
    {
        _pairingRepository = pairingRepository;
        _chatDiscoveryRepository = chatDiscoveryRepository;
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
            Agents = await CargarAgentsConConfiguracionAsync(correo, idCuenta, cancellationToken)
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
        model.Agents = await CargarAgentsConConfiguracionAsync(correo, idCuenta, cancellationToken);

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

        List<RadarAgentDiscoveredChatItem> disponibles =
            await _chatDiscoveryRepository.ListarChatsAsync(
                correo,
                idCuenta,
                idAgent,
                cancellationToken);

        var nombresDisponibles = disponibles
            .Select(x => x.Nombre)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var model = new RadarAgentConfigurationEditViewModel
        {
            IdAgent = configuracion.IdAgent,
            NombreAgent = configuracion.NombreAgent,
            EquipoNombre = configuracion.EquipoNombre,
            Activo = configuracion.Activo && !configuracion.RevocadoUtc.HasValue,
            Configurada = configuracion.Configurada,
            ChatsSeleccionados = configuracion.ChatsMonitoreados
                .Where(nombresDisponibles.Contains)
                .ToList(),
            ChatsTexto = string.Join(
                Environment.NewLine,
                configuracion.ChatsMonitoreados.Where(x => !nombresDisponibles.Contains(x))),
            ChatsDisponibles = disponibles,
            ChatsDetectadosUtc = disponibles.Count == 0
                ? null
                : disponibles.Max(x => x.UltimoVistoUtc),
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

        var chatsManuales = (model.ChatsTexto ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chats = (model.ChatsSeleccionados ?? [])
            .Concat(chatsManuales)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        if (chats.Count == 0)
            ModelState.AddModelError(nameof(model.ChatsTexto), "Selecciona o agrega al menos un chat para monitorear.");

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
            await CargarChatsDisponiblesAsync(model, correo, idCuenta, cancellationToken);
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

    private async Task CargarChatsDisponiblesAsync(
        RadarAgentConfigurationEditViewModel model,
        string correo,
        int idCuenta,
        CancellationToken cancellationToken)
    {
        model.ChatsDisponibles = await _chatDiscoveryRepository.ListarChatsAsync(
            correo,
            idCuenta,
            model.IdAgent,
            cancellationToken);
        model.ChatsDetectadosUtc = model.ChatsDisponibles.Count == 0
            ? null
            : model.ChatsDisponibles.Max(x => x.UltimoVistoUtc);
    }

    private async Task<List<RadarAgentDeviceListItem>> CargarAgentsConConfiguracionAsync(
        string correo,
        int idCuenta,
        CancellationToken cancellationToken)
    {
        List<RadarAgentDeviceListItem> agents = await _pairingRepository.ListarAgentsAsync(
            correo,
            idCuenta,
            cancellationToken);

        foreach (RadarAgentDeviceListItem agent in agents)
        {
            RadarAgentConfiguration? configuracion = await _pairingRepository.ObtenerConfiguracionAsync(
                correo,
                idCuenta,
                agent.IdAgent,
                cancellationToken);

            if (configuracion is null)
                continue;

            agent.Configurada = configuracion.Configurada;
            agent.ChatsMonitoreadosCount = configuracion.ChatsMonitoreados.Count;
            agent.DestinoAlertas = configuracion.DestinoAlertas;
            agent.IntervaloRevisionMs = configuracion.IntervaloRevisionMs;
        }

        return agents;
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
