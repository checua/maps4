using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public sealed class JsonRadarKnowledgeProvider : IRadarKnowledgeProvider
{
    private readonly string _path;

    public JsonRadarKnowledgeProvider(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Se requiere la ruta del conocimiento RADAR.", nameof(path));

        _path = path;
    }

    public async Task<string?> ConstruirContextoAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var document = await CargarDocumentoAsync(cancellationToken);
        if (document is null)
            return null;

        var texto = Normalizar(mensaje.TextoOriginal);
        var terminos = ObtenerTerminosRelevantes(document, texto);

        var ejemplos = document.Ejemplos
            .Where(x => x.Activo && x.ConfirmadoPorHumano)
            .Where(x => CoincideEjemplo(x, texto))
            .Take(3)
            .ToList();

        if (terminos.Count == 0 && ejemplos.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("CONOCIMIENTO RADAR APROBADO POR HUMANO PARA ESTE MENSAJE:");
        sb.AppendLine("Usa este conocimiento como contexto de dominio. No inventes reglas que no estén escritas aquí.");

        if (terminos.Count > 0)
        {
            sb.AppendLine("Términos relevantes:");
            foreach (var termino in terminos)
            {
                sb.Append("- ").Append(termino.Termino);

                if (!string.IsNullOrWhiteSpace(termino.Categoria))
                    sb.Append(" [").Append(termino.Categoria).Append(']');

                if (!string.IsNullOrWhiteSpace(termino.ValorCanonico))
                    sb.Append(" => ").Append(termino.ValorCanonico);

                if (!string.IsNullOrWhiteSpace(termino.TipoBaseCanonico))
                    sb.Append("; tipo base => ").Append(termino.TipoBaseCanonico);

                if (!string.IsNullOrWhiteSpace(termino.Instruccion))
                    sb.Append(". ").Append(termino.Instruccion.Trim());

                sb.AppendLine();
            }
        }

        if (ejemplos.Count > 0)
        {
            sb.AppendLine("Ejemplos corregidos por una persona autorizada:");
            foreach (var ejemplo in ejemplos)
            {
                sb.AppendLine($"- Mensaje: {ejemplo.Mensaje}");
                sb.AppendLine($"  Interpretación correcta: {ejemplo.InterpretacionCorrecta}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<IReadOnlyList<RadarKnowledgeTerm>> ObtenerTerminosRelevantesAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var document = await CargarDocumentoAsync(cancellationToken);
        if (document is null)
            return [];

        return ObtenerTerminosRelevantes(document, Normalizar(mensaje.TextoOriginal));
    }

    private async Task<RadarKnowledgeDocument?> CargarDocumentoAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<RadarKnowledgeDocument>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static List<RadarKnowledgeTerm> ObtenerTerminosRelevantes(
        RadarKnowledgeDocument document,
        string textoNormalizado) =>
        document.Terminos
            .Where(x => x.Activo && x.ConfirmadoPorHumano)
            .Where(x => CoincideTermino(x, textoNormalizado))
            .Take(12)
            .ToList();

    private static bool CoincideTermino(RadarKnowledgeTerm termino, string textoNormalizado)
    {
        var candidatos = new List<string> { termino.Termino };
        candidatos.AddRange(termino.Alias);

        return candidatos
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalizar)
            .Where(x => x.Length >= 2)
            .Any(x => ContieneFrase(textoNormalizado, x));
    }

    private static bool CoincideEjemplo(RadarTrainingExample ejemplo, string textoNormalizado)
    {
        if (ejemplo.Activadores.Count > 0)
        {
            return ejemplo.Activadores
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Normalizar)
                .Any(x => x.Length >= 2 && ContieneFrase(textoNormalizado, x));
        }

        var tokensEjemplo = Normalizar(ejemplo.Mensaje)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 5)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return tokensEjemplo.Count >= 2 &&
               tokensEjemplo.Count(x => textoNormalizado.Contains(x, StringComparison.OrdinalIgnoreCase)) >= 2;
    }

    private static bool ContieneFrase(string texto, string frase)
    {
        if (frase.Contains(' '))
            return texto.Contains(frase, StringComparison.OrdinalIgnoreCase);

        return Regex.IsMatch(
            texto,
            $@"(?:^|\s){Regex.Escape(frase)}(?:$|\s)",
            RegexOptions.IgnoreCase);
    }

    private static string Normalizar(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);

        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }

        return Regex.Replace(
                sb.ToString().Normalize(NormalizationForm.FormC),
                @"[^a-z0-9]+",
                " ")
            .Trim();
    }
}
