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

    private static int _actualizacionPeriodicaIniciada;
    private static string? _ultimaFirma;

    public static async Task DescubrirYReportarAsync(
        IPage page,
        RadarAgentConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (config is null)
            return;

        try
        {
            await DescubrirYReportarUnaVezAsync(
                page,
                config,
                mostrarEstado: true,
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
            IniciarActualizacionPeriodica(page.Context, config);
        }
    }

    private static void IniciarActualizacionPeriodica(
        IBrowserContext context,
        RadarAgentConfig config)
    {
        if (Interlocked.Exchange(ref _actualizacionPeriodicaIniciada, 1) == 1)
            return;

        TimeSpan intervalo = ObtenerIntervaloActualizacion();
        Console.WriteLine($"  🔄 Catálogo de chats: actualización automática cada {intervalo.TotalMinutes:0} min.");
        _ = Task.Run(() => EjecutarActualizacionPeriodicaAsync(context, config, intervalo));
    }

    private static async Task EjecutarActualizacionPeriodicaAsync(
        IBrowserContext context,
        RadarAgentConfig config,
        TimeSpan intervalo)
    {
        while (true)
        {
            try
            {
                await Task.Delay(intervalo);

                IPage? page = null;
                try
                {
                    page = await context.NewPageAsync();
                    await page.GotoAsync(
                        "https://web.whatsapp.com",
                        new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

                    await page.Locator("[data-testid='chat-list']").WaitForAsync(
                        new LocatorWaitForOptions { Timeout = 60_000 });

                    await DescubrirYReportarUnaVezAsync(
                        page,
                        config,
                        mostrarEstado: false,
                        CancellationToken.None);
                }
                finally
                {
                    if (page is not null && !page.IsClosed)
                    {
                        try { await page.CloseAsync(); } catch { }
                    }
                }
            }
            catch (PlaywrightException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Actualización periódica de chats no disponible: {ex.Message}");
            }
        }
    }

    private static async Task DescubrirYReportarUnaVezAsync(
        IPage page,
        RadarAgentConfig config,
        bool mostrarEstado,
        CancellationToken cancellationToken)
    {
        List<string> chats = await DescubrirAsync(page, cancellationToken);
        if (chats.Count == 0)
        {
            if (mostrarEstado)
                Console.WriteLine("  ⚠ RADAR no encontró chats para reportar a RSMaps.");
            return;
        }

        string firma = string.Join('\u001F', chats);
        bool cambio = !string.Equals(_ultimaFirma, firma, StringComparison.Ordinal);

        bool reportados = await RadarAgentBackendClient.ReportarChatsDisponiblesAsync(
            config,
            chats,
            cancellationToken);

        if (!reportados)
        {
            Console.WriteLine($"  ⚠ Se detectaron {chats.Count} chat(s), pero no fue posible reportarlos a RSMaps.");
            return;
        }

        _ultimaFirma = firma;

        if (mostrarEstado)
        {
            Console.WriteLine($"  🔎 Chats WhatsApp detectados y reportados a RSMaps: {chats.Count}.");
        }
        else if (cambio)
        {
            Console.WriteLine($"  🔄 Catálogo WhatsApp actualizado en RSMaps: {chats.Count} chat(s).");
        }
    }

    private static async Task<List<string>> DescubrirAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ILocator lista = page.Locator("[data-testid='chat-list']").First;
        ILocator titulos = page.Locator("[data-testid='cell-frame-title']");

        try
        {
            await lista.HoverAsync();
            int rondasSinNuevos = 0;

            for (int ronda = 0; ronda < 40 && rondasSinNuevos < 5; ronda++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int antes = nombres.Count;
                int total = await titulos.CountAsync();

                for (int i = 0; i < total; i++)
                {
                    ILocator titulo = titulos.Nth(i);
                    try
                    {
                        if (!await titulo.IsVisibleAsync())
                            continue;

                        string nombre = LimpiarNombreChat((await titulo.InnerTextAsync()).Trim());
                        if (string.IsNullOrWhiteSpace(nombre))
                            continue;

                        nombres.Add(nombre[..Math.Min(nombre.Length, 300)]);
                    }
                    catch
                    {
                        // WhatsApp virtualiza la lista; un elemento puede desaparecer mientras se recorre.
                    }
                }

                rondasSinNuevos = nombres.Count == antes ? rondasSinNuevos + 1 : 0;
                await page.Mouse.WheelAsync(0, 900);
                await Task.Delay(180, cancellationToken);
            }

            try
            {
                await lista.EvaluateAsync("el => { el.scrollTop = 0; }");
                await Task.Delay(250, cancellationToken);
            }
            catch { }
        }
        catch (Exception ex) when (ex is PlaywrightException or OperationCanceledException)
        {
            if (ex is OperationCanceledException)
                throw;

            Console.WriteLine($"  ⚠ Descubrimiento de chats no disponible: {ex.Message}");
        }

        return nombres
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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
        return Regex.Replace(limpio, @"\s+", " ").Trim();
    }
}
