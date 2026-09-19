using Microsoft.Playwright;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class WhatsAppDomLab
{
    private const string WhatsAppUrl = "https://web.whatsapp.com/";
    // Evidencia DOM observada en el Lab: el mensaje usa
    // [data-testid^='conv-msg-'][data-id] y su contenido [data-testid='msg-container'].
    // Reply: [data-testid='quoted-message']; el texto citado es un selectable-text
    // descendiente del quote y el texto propio queda fuera del quote dentro de msg-container.
    // Forwarded: [data-testid='forwarded-header']. Metadata: [data-testid='msg-meta'].
    // No están confirmados AutorCitado, MessageIdCitado ni el timestamp estructurado exacto.
    private const string MessageSelector = "[data-testid^='conv-msg-'][data-id]";
    private const int DefaultTimeoutMinutes = 10;

    public static async Task RunAsync()
    {
        TimeSpan timeout = ObtenerTimeout();
        using var cancellation = new CancellationTokenSource(timeout);
        bool cancelledByUser = false;
        ConsoleCancelEventHandler cancelHandler = (_, args) =>
        {
            args.Cancel = true;
            cancelledByUser = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            await RunCoreAsync(timeout, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.WriteLine(cancelledByUser
                ? "DOM Lab cancelado limpiamente por el usuario."
                : $"DOM Lab agotó el timeout de {timeout.TotalMinutes:0.#} minutos sin tocar producción.");
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task RunCoreAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        string domLabRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RSMaps", "RadarAgent", "DomLab");
        string? reusableProfile = Environment.GetEnvironmentVariable("RADAR_DOM_LAB_PROFILE")?.Trim();
        string profile;
        string root;

        if (string.IsNullOrWhiteSpace(reusableProfile))
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            root = Path.Combine(domLabRoot, timestamp);
            profile = Path.Combine(root, "WhatsAppProfile");
            ValidarPerfilLab(profile, domLabRoot);
            if (Directory.Exists(root))
                throw new InvalidOperationException($"La raíz temporal del DOM Lab ya existe: {root}");
        }
        else
        {
            profile = Path.GetFullPath(reusableProfile);
            ValidarPerfilLab(profile, domLabRoot);
            if (!Directory.Exists(profile))
                throw new InvalidOperationException($"El perfil reutilizable del DOM Lab no existe: {profile}");

            root = Directory.GetParent(profile)?.FullName
                ?? throw new InvalidOperationException("No se pudo determinar la raíz del perfil Lab.");
        }

        string output = Path.Combine(root, "output");

        Directory.CreateDirectory(profile);
        Directory.CreateDirectory(output);

        Console.WriteLine("WHATSAPP DOM SAFE LAB");
        Console.WriteLine($"Perfil temporal: {profile}");
        Console.WriteLine("El runner no selecciona chats ni envía mensajes.");
        Console.WriteLine($"Timeout máximo del Lab: {timeout.TotalMinutes:0.#} minutos.");

        using IPlaywright playwright = await Playwright.CreateAsync();
        await using IBrowserContext context =
            await playwright.Chromium.LaunchPersistentContextAsync(
                profile,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    ViewportSize = null
                });

        IPage page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        await page.GotoAsync(
            WhatsAppUrl,
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForFunctionAsync("() => document.readyState === 'complete'");
        await page.Locator("#app").WaitForAsync(
            new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 30_000 });
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

        bool sidebarDiscovery = string.Equals(
            Environment.GetEnvironmentVariable("RADAR_DOM_LAB_DISCOVERY")?.Trim(),
            "sidebar",
            StringComparison.OrdinalIgnoreCase);

        if (sidebarDiscovery)
        {
            Console.WriteLine("Esperando que la lista lateral de chats esté materializada.");
            await EsperarSidebarListoAsync(page, cancellationToken);
            Console.Write("Sidebar listo para discovery. Presione ENTER para capturar: ");
        }
        else
        {
            Console.WriteLine("Abra manualmente el chat de prueba y deje visibles los tres mensajes.");
            await EsperarChatListoAsync(page, cancellationToken);
            Console.Write("Chat listo para discovery. Presione ENTER para capturar: ");
        }

        await LeerEnterAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        ILocator messages = page.Locator(MessageSelector);
        int count = await messages.CountAsync();
        string discoveryPath = Path.Combine(output, "discovery.json");
        DiscoveryResult discovery = await DescubrirAsync(page, cancellationToken);
        await GuardarSanitizadoAsync(discovery, discoveryPath, cancellationToken);

        Console.WriteLine($"Selector principal: {count} mensajes.");
        foreach (var item in discovery.Counts)
            Console.WriteLine($"{item.Selector}: {item.Count}");
        Console.WriteLine($"Discovery sanitizado: {discoveryPath}");
        Console.Write("Presione ENTER para cerrar el Lab: ");
        await LeerEnterAsync(cancellationToken);
    }

    private static async Task EsperarSidebarListoAsync(IPage page, CancellationToken cancellationToken)
    {
        const string titles = "[data-testid='cell-frame-title']";
        const string rows = "[role='row']";
        SidebarReadinessState? lastReported = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SidebarReadinessState first = await LeerSidebarReadinessAsync(page, titles, rows);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            SidebarReadinessState second = await LeerSidebarReadinessAsync(page, titles, rows);
            bool stable = first.Titles > 0 && first.Rows > 0 && second.Titles > 0 && second.Rows > 0;
            var current = second with { Stable = stable };

            if (lastReported != current)
            {
                Console.WriteLine($"SIDEBAR_TITULOS = {current.Titles}");
                Console.WriteLine($"SIDEBAR_FILAS = {current.Rows}");
                Console.WriteLine($"SIDEBAR_ESTABLE = {(current.Stable ? "SI" : "NO")}");
                lastReported = current;
            }

            if (stable)
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private static async Task<SidebarReadinessState> LeerSidebarReadinessAsync(
        IPage page,
        string titles,
        string rows) =>
        new(
            await page.Locator(titles).CountAsync(),
            await page.Locator(rows).CountAsync(),
            Stable: false);

    private static async Task EsperarChatListoAsync(IPage page, CancellationToken cancellationToken)
    {
        const string conversationHeader = "[data-testid='conversation-info-header-chat-title']";
        const string materializedMessages = "[data-testid^='conv-msg-']";
        ReadinessState? lastReported = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadinessState first = await LeerReadinessAsync(
                page,
                conversationHeader,
                materializedMessages);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            ReadinessState second = await LeerReadinessAsync(
                page,
                conversationHeader,
                materializedMessages);

            bool stable = first.ConversationOpen &&
                first.MaterializedMessages >= 3 &&
                second.ConversationOpen &&
                second.MaterializedMessages >= 3;
            var current = second with { Stable = stable };

            if (lastReported != current)
            {
                Console.WriteLine($"CONVERSACION_ABIERTA = {(current.ConversationOpen ? "SI" : "NO")}");
                Console.WriteLine($"MENSAJES_MATERIALIZADOS = {current.MaterializedMessages}");
                Console.WriteLine($"ESTABLE = {(current.Stable ? "SI" : "NO")}");
                if (!current.Stable)
                    Console.WriteLine("Abra manualmente el chat de prueba y deje visibles los tres mensajes.");

                lastReported = current;
            }

            if (stable)
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private static async Task<ReadinessState> LeerReadinessAsync(
        IPage page,
        string conversationHeader,
        string materializedMessages)
    {
        bool conversationOpen = await page.Locator(conversationHeader).CountAsync() > 0;
        int messages = await page.Locator(materializedMessages).CountAsync();
        return new ReadinessState(conversationOpen, messages, Stable: false);
    }

    private static async Task<DiscoveryResult> DescubrirAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        string[] selectors =
        [
            "[data-testid='chat-list']",
            "[data-testid*='chat' i]",
            "[data-testid='cell-frame-title']",
            "[role='grid']",
            "[role='list']",
            "[role='row']",
            "[role='listitem']",
            "[aria-label]",
            "[data-testid^='conv-msg-'][data-id]",
            "[data-testid^='conv-msg-']",
            "[data-id]",
            "div[data-id]",
            "[data-testid*='msg' i]",
            "[data-testid*='message' i]",
            "[data-testid*='conversation' i]"
        ];

        var counts = new List<DiscoveryCount>();
        foreach (string selector in selectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            counts.Add(new DiscoveryCount(selector, await page.Locator(selector).CountAsync()));
        }

        JsonElement diagnostics = await page.EvaluateAsync<JsonElement>(
            """
            selectors => {
              const unique = [];
              const seen = new Set();
              const matchedBy = new Map();
              for (const selector of selectors) {
                for (const element of document.querySelectorAll(selector)) {
                  if (!matchedBy.has(element)) matchedBy.set(element, []);
                  matchedBy.get(element).push(selector);
                  if (!seen.has(element)) {
                    seen.add(element);
                    unique.push(element);
                  }
                  if (unique.length >= 160) break;
                }
                if (unique.length >= 160) break;
              }

              unique.sort((a, b) => a === b ? 0 :
                (a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1));

              const structural = value => {
                if (!value || !/^[a-z][a-z0-9_-]{0,63}$/i.test(value)) return '[REDACTADO]';
                return value;
              };
              const describe = (element, index) => {
                let depth = 0;
                for (let current = element.parentElement; current && depth < 50; current = current.parentElement)
                  depth++;
                const parent = element.parentElement;
                const rect = element.getBoundingClientRect();
                const descendantTestIds = Array.from(element.querySelectorAll('[data-testid]'))
                  .slice(0, 12)
                  .map(x => structural(x.getAttribute('data-testid')));
                return {
                  candidate: `C${index + 1}`,
                  domOrder: index + 1,
                  top: Math.round(rect.top * 100) / 100,
                  height: Math.round(rect.height * 100) / 100,
                  matchedSelectors: matchedBy.get(element) ?? [],
                  tagName: element.tagName,
                  depth,
                  attributeNames: element.getAttributeNames(),
                  dataTestId: structural(element.getAttribute('data-testid')),
                  role: structural(element.getAttribute('role')),
                  hasDataId: element.hasAttribute('data-id'),
                  ancestorHasDataId: Boolean(element.parentElement?.closest('[data-id]')),
                  descendantHasDataId: Boolean(element.querySelector('[data-id]')),
                  dataAttributeNames: element.getAttributeNames().filter(x => x.startsWith('data-')),
                  parentTagName: parent?.tagName ?? null,
                  parentDataTestId: structural(parent?.getAttribute('data-testid')),
                  childCount: element.children.length,
                  hasText: (element.textContent ?? '').trim().length > 0,
                  visible: rect.width > 0 && rect.height > 0,
                  descendantRowCount: element.querySelectorAll("[role='row']").length,
                  descendantTitleCount: element.querySelectorAll("[data-testid='cell-frame-title']").length,
                  descendantTestIds
                };
              };

              const candidates = unique.map(describe);
              const primary = Array.from(document.querySelectorAll("[data-testid^='conv-msg-'][data-id]"))
                .map((element, index) => ({ sequence: index + 1, ...describe(element, index) }));
              return { primary, candidates };
            }
            """,
            selectors);

        return new DiscoveryResult(counts, diagnostics);
    }

    private static async Task GuardarSanitizadoAsync<T>(
        T value,
        string path,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions { WriteIndented = true });
        json = SegundaPasadaSanitizacion(json);
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), cancellationToken);
    }

    private static async Task LeerEnterAsync(CancellationToken cancellationToken) =>
        await Task.Run(Console.ReadLine).WaitAsync(cancellationToken);

    private static TimeSpan ObtenerTimeout()
    {
        string? configured = Environment.GetEnvironmentVariable("RADAR_DOM_LAB_TIMEOUT_MINUTES")?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
            return TimeSpan.FromMinutes(DefaultTimeoutMinutes);

        if (!int.TryParse(configured, out int minutes) || minutes is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "RADAR_DOM_LAB_TIMEOUT_MINUTES debe ser un entero entre 1 y 60.");
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private static string SegundaPasadaSanitizacion(string value)
    {
        value = Regex.Replace(
            value,
            @"https?://[^\s\""']+",
            "[URL_REDACTADA]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(
            value,
            @"\+?\d(?:[\s-]*\d){9,14}",
            "[TELEFONO]",
            RegexOptions.CultureInvariant);
        value = Regex.Replace(
            value,
            @"(?<![A-Za-z0-9])[A-Za-z0-9_-]{24,}(?![A-Za-z0-9])",
            "[ID_REDACTADO]",
            RegexOptions.CultureInvariant);
        return value;
    }

    private static void ValidarPerfilLab(string profile, string domLabRoot)
    {
        string full = Path.GetFullPath(profile);
        string allowedRoot = Path.GetFullPath(domLabRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (full.Contains(
                $"{Path.DirectorySeparatorChar}AgentProduccion{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El DOM Lab no puede usar AgentProduccion.");
        }

        if (!full.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"El perfil debe estar bajo la raíz exacta del DOM Lab: {allowedRoot}");
    }


    private sealed record DiscoveryCount(string Selector, int Count);

    private sealed record DiscoveryResult(
        IReadOnlyList<DiscoveryCount> Counts,
        JsonElement Candidates);

    private sealed record ReadinessState(
        bool ConversationOpen,
        int MaterializedMessages,
        bool Stable);

    private sealed record SidebarReadinessState(
        int Titles,
        int Rows,
        bool Stable);
}
