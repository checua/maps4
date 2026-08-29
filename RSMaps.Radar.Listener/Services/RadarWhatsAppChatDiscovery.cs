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

    public static async Task DescubrirYReportarAsync(
        IPage page,
        RadarAgentConfig? config,
        CancellationToken cancellationToken = default)
    {
        if (config is null)
            return;

        try
        {
            List<string> chats = await DescubrirAsync(page, cancellationToken);
            if (chats.Count == 0)
            {
                Console.WriteLine("  ⚠ RADAR no encontró chats para reportar a RSMaps.");
                return;
            }

            bool reportados = await RadarAgentBackendClient.ReportarChatsDisponiblesAsync(
                config,
                chats,
                cancellationToken);

            Console.WriteLine(reportados
                ? $"  🔎 Chats WhatsApp detectados y reportados a RSMaps: {chats.Count}."
                : $"  ⚠ Se detectaron {chats.Count} chat(s), pero no fue posible reportarlos a RSMaps.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Descubrimiento de chats no bloqueante: {ex.Message}");
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

            // Intentamos regresar la lista al inicio para no alterar la navegación normal del Agent.
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

    private static string LimpiarNombreChat(string nombre)
    {
        string limpio = PrefijoNoLeidoEs.Replace(nombre, string.Empty);
        limpio = PrefijoNoLeidoEn.Replace(limpio, string.Empty);
        return Regex.Replace(limpio, @"\s+", " ").Trim();
    }
}
