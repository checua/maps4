using Microsoft.Playwright;
using RSMaps.Radar.Listener.Config;
using System.Text.RegularExpressions;

namespace RSMaps.Radar.Listener.Services;

public static class RadarWhatsAppChatDiscovery
{
    private static readonly Regex PrefijoNoLeidoEs = new(
        @"^\s*\d+\s+mensajes?\s+no\s+le[ií]dos?\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PrefijoNoLeidoEn = new(
        @"^\s*\d+\s+unread\s+messages?\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> ConocidosSesion =
        new(StringComparer.OrdinalIgnoreCase);

    private static DateTime _proximaActualizacionUtc = DateTime.MinValue;
    private static TimeSpan? _intervalo;

    public static async Task DescubrirYReportarAsync(
        IPage page,
        RadarAgentConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (config is null)
            return;

        TimeSpan intervalo = ObtenerIntervaloActualizacion();
        _intervalo = intervalo;

        try
        {
            await DescubrirYReportarUnaVezAsync(
                page,
                config,
                mostrarEstadoInicial: true,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Descubrimiento de chats no bloqueante: {ex.Message}");
        }
        finally
        {
            _proximaActualizacionUtc = DateTime.UtcNow.Add(intervalo);
            Console.WriteLine(
                $"  🔄 Catálogo de chats: actualización automática cada {intervalo.TotalMinutes:0} min " +
                "usando la página principal de WhatsApp.");
        }
    }

    public static async Task ActualizarSiCorrespondeAsync(
        IPage page,
        RadarAgentConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (config is null)
            return;

        TimeSpan intervalo = _intervalo ?? ObtenerIntervaloActualizacion();
        if (DateTime.UtcNow < _proximaActualizacionUtc)
            return;

        _proximaActualizacionUtc = DateTime.UtcNow.Add(intervalo);

        try
        {
            await DescubrirYReportarUnaVezAsync(
                page,
                config,
                mostrarEstadoInicial: false,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Actualización periódica de chats no disponible: {ex.Message}");
        }
    }

    private static async Task DescubrirYReportarUnaVezAsync(
        IPage page,
        RadarAgentConfig config,
        bool mostrarEstadoInicial,
        CancellationToken cancellationToken)
    {
        ResultadoDescubrimiento resultado = await DescubrirAsync(page, cancellationToken);
        if (resultado.Chats.Count == 0)
        {
            if (mostrarEstadoInicial)
                Console.WriteLine("  ⚠ RADAR no encontró chats elegibles para reportar a RSMaps.");
            return;
        }

        bool reportados = await RadarAgentBackendClient.ReportarChatsDisponiblesAsync(
            config,
            resultado.Chats,
            cancellationToken);

        if (!reportados)
        {
            Console.WriteLine(
                $"  ⚠ Se detectaron {resultado.Chats.Count} chat(s) elegibles, " +
                "pero no fue posible reportarlos a RSMaps.");
            return;
        }

        int nuevos = 0;
        foreach (string chat in resultado.Chats)
        {
            if (ConocidosSesion.Add(chat))
                nuevos++;
        }

        string cobertura = resultado.TotalWhatsApp > 0
            ? $"cobertura {resultado.FilasVisitadas}/{resultado.TotalWhatsApp}"
            : $"{resultado.FilasVisitadas} fila(s) observadas";

        string omitidos = resultado.NumerosSinGuardar > 0
            ? $" · {resultado.NumerosSinGuardar} número(s) sin guardar omitido(s)"
            : string.Empty;

        if (mostrarEstadoInicial)
        {
            Console.WriteLine(
                $"  🔎 Catálogo WhatsApp: {resultado.Chats.Count} chat(s) elegible(s) reportado(s) · " +
                $"{cobertura}{omitidos}.");
            return;
        }

        string icono = nuevos > 0 ? "🔄" : "↻";
        string novedad = nuevos > 0
            ? $"{nuevos} nuevo(s) agregado(s)"
            : "0 nuevos";

        Console.WriteLine(
            $"  {icono} Exploración WhatsApp: {resultado.Chats.Count} elegible(s) · " +
            $"{novedad} · catálogo de sesión {ConocidosSesion.Count} · {cobertura}{omitidos}.");
    }

    private static async Task<ResultadoDescubrimiento> DescubrirAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var numerosSinGuardar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filasVisitadas = new HashSet<int>();

        await LimpiarBusquedaAsync(page);

        ILocator grid = page.Locator("[role='grid'][aria-label='Lista de chats'][aria-rowcount]").First;
        if (await grid.CountAsync() == 0)
            grid = page.Locator("[role='grid'][aria-rowcount]").First;

        if (await grid.CountAsync() == 0)
            return new ResultadoDescubrimiento([], 0, 0, 0);

        int totalFilas = 0;
        string? totalRaw = await grid.GetAttributeAsync("aria-rowcount");
        _ = int.TryParse(totalRaw, out totalFilas);

        ILocator filas = grid.Locator("[role='row']");
        double altoFila = 76d;

        try
        {
            if (await filas.CountAsync() > 0)
            {
                altoFila = await filas.First.EvaluateAsync<double>(
                    "el => parseFloat(getComputedStyle(el).height) || 76");
            }
        }
        catch
        {
            altoFila = 76d;
        }

        if (altoFila <= 0)
            altoFila = 76d;

        double altoViewport = await ObtenerAltoViewportAsync(grid);
        int filasPorPantalla = Math.Max(4, (int)Math.Floor(altoViewport / altoFila));
        int pasoFilas = Math.Max(2, filasPorPantalla - 2);

        try
        {
            if (totalFilas > 0)
            {
                for (int indiceObjetivo = 0; indiceObjetivo < totalFilas; indiceObjetivo += pasoFilas)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await MoverAIndiceAsync(grid, indiceObjetivo, altoFila);
                    await Task.Delay(90, cancellationToken);
                    await CapturarFilasMaterializadasAsync(
                        grid,
                        altoFila,
                        totalFilas,
                        nombres,
                        numerosSinGuardar,
                        filasVisitadas);
                }

                int ultimoIndice = Math.Max(0, totalFilas - 1);
                await MoverAIndiceAsync(grid, ultimoIndice, altoFila);
                await Task.Delay(120, cancellationToken);
                await CapturarFilasMaterializadasAsync(
                    grid,
                    altoFila,
                    totalFilas,
                    nombres,
                    numerosSinGuardar,
                    filasVisitadas);
            }
            else
            {
                await CapturarFilasMaterializadasAsync(
                    grid,
                    altoFila,
                    totalFilas,
                    nombres,
                    numerosSinGuardar,
                    filasVisitadas);
            }
        }
        catch (Exception ex) when (ex is PlaywrightException or OperationCanceledException)
        {
            if (ex is OperationCanceledException)
                throw;

            Console.WriteLine($"  ⚠ Descubrimiento de chats no disponible: {ex.Message}");
        }
        finally
        {
            try
            {
                await MoverAIndiceAsync(grid, 0, altoFila);
                await Task.Delay(150, cancellationToken);
            }
            catch
            {
                // La restauración al inicio es de cortesía; no debe bloquear RADAR.
            }
        }

        return new ResultadoDescubrimiento(
            nombres.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
            totalFilas,
            filasVisitadas.Count,
            numerosSinGuardar.Count);
    }

    private static async Task CapturarFilasMaterializadasAsync(
        ILocator grid,
        double altoFila,
        int totalFilas,
        HashSet<string> nombres,
        HashSet<string> numerosSinGuardar,
        HashSet<int> filasVisitadas)
    {
        ILocator filas = grid.Locator("[role='row']");
        int totalMaterializadas = await filas.CountAsync();

        for (int i = 0; i < totalMaterializadas; i++)
        {
            ILocator fila = filas.Nth(i);

            try
            {
                double? desplazamiento = await fila.EvaluateAsync<double?>(
                    "el => { const m = (el.style.transform || '').match(/translateY\\(([-\\d.]+)px\\)/); return m ? parseFloat(m[1]) : null; }");

                if (desplazamiento.HasValue && altoFila > 0)
                {
                    int indice = (int)Math.Round(desplazamiento.Value / altoFila);
                    if (indice >= 0 && (totalFilas <= 0 || indice < totalFilas))
                        filasVisitadas.Add(indice);
                }

                ILocator titulo = fila.Locator("[data-testid='cell-frame-title']").First;
                if (await titulo.CountAsync() == 0)
                    continue;

                string nombre = LimpiarNombreChat((await titulo.InnerTextAsync()).Trim());
                if (string.IsNullOrWhiteSpace(nombre))
                    continue;

                nombre = nombre[..Math.Min(nombre.Length, 300)];

                if (EsNumeroSinGuardar(nombre))
                {
                    numerosSinGuardar.Add(nombre);
                    continue;
                }

                nombres.Add(nombre);
            }
            catch
            {
                // WhatsApp recicla filas mientras cambia el scroll; la siguiente posición las vuelve a observar.
            }
        }
    }

    private static async Task<double> ObtenerAltoViewportAsync(ILocator grid)
    {
        try
        {
            double alto = await grid.EvaluateAsync<double>(@"
                el => {
                    let p = el.parentElement;
                    while (p) {
                        const style = getComputedStyle(p);
                        const scrollable = p.scrollHeight > p.clientHeight + 8;
                        const overflow = style.overflowY === 'auto' || style.overflowY === 'scroll';
                        if (scrollable && overflow)
                            return p.clientHeight;
                        p = p.parentElement;
                    }
                    return Math.min(window.innerHeight || 700, 700);
                }");

            return alto > 0 ? alto : 700d;
        }
        catch
        {
            return 700d;
        }
    }

    private static async Task MoverAIndiceAsync(
        ILocator grid,
        int indice,
        double altoFila)
    {
        double top = Math.Max(0, indice * altoFila);

        bool movido = await grid.EvaluateAsync<bool>(@"
            (el, top) => {
                let p = el.parentElement;
                while (p) {
                    const style = getComputedStyle(p);
                    const scrollable = p.scrollHeight > p.clientHeight + 8;
                    const overflow = style.overflowY === 'auto' || style.overflowY === 'scroll';
                    if (scrollable && overflow) {
                        p.scrollTop = top;
                        return true;
                    }
                    p = p.parentElement;
                }
                return false;
            }", top);

        if (!movido)
        {
            await grid.HoverAsync();
            await grid.EvaluateAsync("el => el.scrollIntoView({ block: 'start' })");
        }
    }

    private static async Task LimpiarBusquedaAsync(IPage page)
    {
        try
        {
            ILocator contenedor = page.Locator("[data-testid='chat-list-search-container']");
            if (await contenedor.CountAsync() > 0)
            {
                ILocator input = contenedor.Locator("[contenteditable='true']").First;
                if (await input.CountAsync() == 0)
                    input = contenedor.Locator("[role='textbox']").First;

                if (await input.CountAsync() > 0 && await input.IsVisibleAsync())
                    await input.FillAsync(string.Empty);
            }

            await page.Keyboard.PressAsync("Escape");
            await Task.Delay(200);
        }
        catch
        {
            // La limpieza es preventiva; el descubrimiento puede continuar si WhatsApp cambia el selector.
        }
    }

    private static TimeSpan ObtenerIntervaloActualizacion()
    {
        string? raw = Environment.GetEnvironmentVariable("RADAR_CHAT_DISCOVERY_MINUTES");
        if (int.TryParse(raw, out int minutos))
            return TimeSpan.FromMinutes(Math.Clamp(minutos, 1, 24 * 60));

        return TimeSpan.FromMinutes(15);
    }

    private static string LimpiarNombreChat(string nombre)
    {
        string limpio = PrefijoNoLeidoEs.Replace(nombre, string.Empty);
        limpio = PrefijoNoLeidoEn.Replace(limpio, string.Empty);
        limpio = Regex.Replace(limpio, @"\s+", " ").Trim();
        limpio = Regex.Replace(limpio, @"(?<!\s)\(Tú\)$", " (Tú)", RegexOptions.IgnoreCase);
        return limpio;
    }

    private static bool EsNumeroSinGuardar(string nombre)
    {
        int digitos = 0;

        foreach (char c in nombre)
        {
            if (char.IsDigit(c))
            {
                digitos++;
                continue;
            }

            if (char.IsWhiteSpace(c) || c is '+' or '-' or '(' or ')' or '.' or '\u00A0')
                continue;

            return false;
        }

        return digitos >= 7;
    }

    private sealed record ResultadoDescubrimiento(
        List<string> Chats,
        int TotalWhatsApp,
        int FilasVisitadas,
        int NumerosSinGuardar);
}
