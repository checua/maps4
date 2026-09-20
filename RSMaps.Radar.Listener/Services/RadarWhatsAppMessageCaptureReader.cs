using Microsoft.Playwright;
using RSMaps.Radar.Listener.Models;
using System.Text.Json;

namespace RSMaps.Radar.Listener.Services;

public static class RadarWhatsAppMessageCaptureReader
{
    public static async Task<RadarWhatsAppMessageCapture> ReadAsync(
        ILocator message,
        string chatOrigen,
        string? autorActual = null,
        string? telefonoActual = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(chatOrigen);

        try
        {
            JsonElement snapshot = await message.EvaluateAsync<JsonElement>(
                """
                message => {
                  const containerSelector = "[data-testid='msg-container']";
                  const quoteSelector = "[data-testid='quoted-message']";
                  const forwardedSelector = "[data-testid='forwarded-header']";
                  const selectableSelector = '.selectable-text';

                  if (!message.isConnected)
                    return { status: 'DOM_CHANGED' };

                  const containers = Array.from(message.querySelectorAll(containerSelector));
                  if (containers.length === 0)
                    return { status: 'MSG_CONTAINER_NOT_FOUND', messageId: message.getAttribute('data-id') };
                  if (containers.length > 1)
                    return { status: 'MSG_CONTAINER_MULTIPLE', messageId: message.getAttribute('data-id') };

                  const container = containers[0];
                  const quotes = Array.from(container.querySelectorAll(quoteSelector));
                  const forwarded = container.querySelector(forwardedSelector) !== null;
                  if (quotes.length > 1) {
                    return {
                      status: 'QUOTE_MULTIPLE',
                      messageId: message.getAttribute('data-id'),
                      hasQuote: true,
                      forwarded
                    };
                  }

                  const isVisibleElement = element => {
                    if (!element.isConnected) return false;
                    for (let current = element; current; current = current.parentElement) {
                      if (current.hidden || current.getAttribute('aria-hidden') === 'true') return false;
                      const style = getComputedStyle(current);
                      if (style.display === 'none' ||
                          style.visibility === 'hidden' ||
                          style.visibility === 'collapse' ||
                          style.contentVisibility === 'hidden' ||
                          Number(style.opacity) === 0) return false;
                    }
                    return Array.from(element.getClientRects())
                      .some(rect => rect.width > 0 && rect.height > 0);
                  };

                  const isVisibleText = node => {
                    const parent = node.parentElement;
                    if (!parent || !isVisibleElement(parent)) return false;
                    const range = document.createRange();
                    range.selectNodeContents(node);
                    return Array.from(range.getClientRects())
                      .some(rect => rect.width > 0 && rect.height > 0);
                  };

                  const extract = (root, includeQuoted) => {
                    const selectables = Array.from(root.querySelectorAll(selectableSelector))
                      .filter(isVisibleElement);
                    const roots = selectables.filter(node =>
                      !selectables.some(other => other !== node && other.contains(node)));
                    const fragments = [];

                    for (const selectable of roots) {
                      if (!isVisibleElement(selectable)) continue;
                      const walker = document.createTreeWalker(selectable, NodeFilter.SHOW_TEXT);
                      const parts = [];
                      for (let textNode = walker.nextNode(); textNode; textNode = walker.nextNode()) {
                        const insideQuote = textNode.parentElement?.closest(quoteSelector) !== null;
                        if (insideQuote !== includeQuoted || !isVisibleText(textNode)) continue;
                        parts.push(textNode.nodeValue ?? '');
                      }
                      const fragment = parts.join('').trim();
                      if (fragment.length > 0) fragments.push(fragment);
                    }

                    return fragments;
                  };

                  const visibleSelectables = Array.from(container.querySelectorAll(selectableSelector))
                    .filter(isVisibleElement).length;
                  const ownFragments = extract(container, false);
                  const quotedFragments = quotes.length === 1 ? extract(quotes[0], true) : [];

                  if (visibleSelectables === 0 && quotes.length === 0 && !forwarded) {
                    return {
                      status: 'TEXT_STRUCTURE_AMBIGUOUS',
                      messageId: message.getAttribute('data-id')
                    };
                  }

                  return {
                    status: 'OK',
                    messageId: message.getAttribute('data-id'),
                    hasQuote: quotes.length === 1,
                    forwarded,
                    ownFragments,
                    quotedFragments
                  };
                }
                """);

            return CrearDesdeSnapshot(snapshot, chatOrigen, autorActual, telefonoActual);
        }
        catch (PlaywrightException ex) when (EsErrorPlaywrightTransitorio(ex))
        {
            return CrearCapturaNoConfirmada(
                "PLAYWRIGHT_TRANSIENT",
                messageId: null,
                chatOrigen,
                autorActual,
                telefonoActual);
        }
    }

    private static bool EsErrorPlaywrightTransitorio(PlaywrightException exception)
    {
        string message = exception.Message;
        string[] transientMarkers =
        [
            "has been closed",
            "Target closed",
            "not attached",
            "detached",
            "Execution context was destroyed",
            "Cannot find context",
            "Timeout"
        ];

        return transientMarkers.Any(marker =>
            message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static RadarWhatsAppMessageCapture CrearDesdeSnapshot(
        JsonElement snapshot,
        string chatOrigen,
        string? autorActual,
        string? telefonoActual)
    {
        string status = LeerString(snapshot, "status") ?? "TEXT_STRUCTURE_AMBIGUOUS";
        string? messageId = NormalizarValor(LeerString(snapshot, "messageId"));
        bool tieneCita = LeerBooleano(snapshot, "hasQuote");
        bool esReenviado = LeerBooleano(snapshot, "forwarded");

        if (!string.Equals(status, "OK", StringComparison.Ordinal))
        {
            return CrearCapturaNoConfirmada(
                status,
                messageId,
                chatOrigen,
                autorActual,
                telefonoActual,
                tieneCita,
                esReenviado);
        }

        if (messageId is null)
        {
            return CrearCapturaNoConfirmada(
                "MESSAGE_ID_MISSING",
                messageId: null,
                chatOrigen,
                autorActual,
                telefonoActual,
                tieneCita,
                esReenviado);
        }

        return new RadarWhatsAppMessageCapture
        {
            Confirmado = true,
            MessageId = messageId,
            ChatOrigen = chatOrigen,
            AutorActual = NormalizarValor(autorActual),
            TelefonoActual = NormalizarValor(telefonoActual),
            TextoPropio = UnirFragmentos(snapshot, "ownFragments"),
            TieneCita = tieneCita,
            TextoCitado = NormalizarValor(UnirFragmentos(snapshot, "quotedFragments")),
            EsReenviado = esReenviado,
            TimestampMensaje = null
        };
    }

    private static RadarWhatsAppMessageCapture CrearCapturaNoConfirmada(
        string motivo,
        string? messageId,
        string chatOrigen,
        string? autorActual,
        string? telefonoActual,
        bool tieneCita = false,
        bool esReenviado = false) =>
        new()
        {
            Confirmado = false,
            MotivoNoConfirmado = motivo,
            MessageId = messageId,
            ChatOrigen = chatOrigen,
            AutorActual = NormalizarValor(autorActual),
            TelefonoActual = NormalizarValor(telefonoActual),
            TieneCita = tieneCita,
            EsReenviado = esReenviado,
            TimestampMensaje = null
        };

    private static string UnirFragmentos(JsonElement snapshot, string propertyName)
    {
        if (!snapshot.TryGetProperty(propertyName, out JsonElement fragments) ||
            fragments.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        return string.Join(
            "\n",
            fragments
                .EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? NormalizarValor(x.GetString()) : null)
                .Where(x => x is not null)
                .Cast<string>());
    }

    private static string? LeerString(JsonElement snapshot, string propertyName) =>
        snapshot.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool LeerBooleano(JsonElement snapshot, string propertyName) =>
        snapshot.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static string? NormalizarValor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalized = value
            .Replace("\u200B", "", StringComparison.Ordinal)
            .Replace("\u200C", "", StringComparison.Ordinal)
            .Replace("\u200D", "", StringComparison.Ordinal)
            .Replace("\u200E", "", StringComparison.Ordinal)
            .Replace("\u200F", "", StringComparison.Ordinal)
            .Replace("\u2060", "", StringComparison.Ordinal)
            .Replace("\uFEFF", "", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
