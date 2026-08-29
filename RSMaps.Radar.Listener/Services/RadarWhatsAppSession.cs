using Microsoft.Playwright;

namespace RSMaps.Radar.Listener.Services;

public static class RadarWhatsAppSession
{
    private const string WhatsAppUrl = "https://web.whatsapp.com";

    public static async Task<IPage> ObtenerPaginaActivaAsync(
        IBrowserContext context,
        IPage? actual = null,
        bool mostrarRecuperacion = false,
        CancellationToken cancellationToken = default)
    {
        var candidatas = context.Pages
            .Where(x => !x.IsClosed && EsWhatsApp(x.Url))
            .ToList();

        IPage? page = actual is not null && !actual.IsClosed && EsWhatsApp(actual.Url)
            ? actual
            : candidatas.FirstOrDefault();

        page ??= context.Pages.FirstOrDefault(x => !x.IsClosed);
        page ??= await context.NewPageAsync();

        if (!EsWhatsApp(page.Url))
        {
            await page.GotoAsync(
                WhatsAppUrl,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        }

        await page.Locator("[data-testid='chat-list']").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 60_000 });

        foreach (IPage duplicada in context.Pages
                     .Where(x => !x.IsClosed && !ReferenceEquals(x, page) && EsWhatsApp(x.Url))
                     .ToList())
        {
            try
            {
                await duplicada.CloseAsync();
            }
            catch
            {
                // Si WhatsApp ya cerró la pestaña duplicada, no hay nada más que hacer.
            }
        }

        if (mostrarRecuperacion)
            Console.WriteLine("  ♻ Página principal de WhatsApp recuperada.");

        return page;
    }

    private static bool EsWhatsApp(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith(WhatsAppUrl, StringComparison.OrdinalIgnoreCase);
}
