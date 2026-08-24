using RSMaps.Radar.Listener.Models;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;

namespace RSMaps.Radar.Listener.Services;

public static class RsMapsMatchingClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static string Endpoint =>
        Environment.GetEnvironmentVariable("RSMAPS_MATCHING_URL")?.Trim()
        ?? "http://localhost:5102/api/radar/matching/local";

    public static async Task<string> ConstruirResumenAsync(SolicitudInmobiliaria solicitud)
    {
        try
        {
            using var response = await Http.PostAsJsonAsync(Endpoint, solicitud);

            if (!response.IsSuccessStatusCode)
            {
                var detalle = await response.Content.ReadAsStringAsync();
                return $"COINCIDENCIAS RSMAPS\n⚠ No fue posible comparar ahora ({(int)response.StatusCode})."
                    + (string.IsNullOrWhiteSpace(detalle) ? string.Empty : $"\n{Recortar(detalle, 180)}");
            }

            var resultado = await response.Content.ReadFromJsonAsync<MatchingResponse>();
            if (resultado is null)
                return "COINCIDENCIAS RSMAPS\n⚠ RSMaps respondió sin datos de matching.";

            if (resultado.TotalCandidatos <= 0 || resultado.Resultados.Count == 0)
            {
                return $"COINCIDENCIAS RSMAPS\n❌ Sin coincidencias útiles actuales.\nInventario evaluado: {resultado.TotalInventarioEvaluado}.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("COINCIDENCIAS RSMAPS");

            foreach (var item in resultado.Resultados.Take(3))
            {
                var icono = item.Puntuacion >= 85 ? "🟢" : item.Puntuacion >= 70 ? "🟡" : "⚪";
                sb.AppendLine($"{icono} {item.Puntuacion}% · Inmueble #{item.IdInmueble}");

                var datos = new List<string>();
                if (!string.IsNullOrWhiteSpace(item.TipoNombre))
                    datos.Add(item.TipoNombre.Trim());
                if (item.Precio.HasValue && item.Precio.Value > 0)
                    datos.Add(item.Precio.Value.ToString("C0", CultureInfo.GetCultureInfo("es-MX")));
                if (item.Recamaras.HasValue)
                    datos.Add($"{item.Recamaras} rec");
                if (item.BanosCompletos.HasValue)
                    datos.Add($"{item.BanosCompletos} baños");

                if (datos.Count > 0)
                    sb.AppendLine(string.Join(" · ", datos));

                if (!string.IsNullOrWhiteSpace(item.Direccion))
                    sb.AppendLine(item.Direccion.Trim());

                var motivo = item.Coincidencias.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(motivo))
                    sb.AppendLine($"✓ {motivo}");
            }

            sb.Append($"Candidatos: {resultado.TotalCandidatos} de {resultado.TotalInventarioEvaluado} evaluados.");
            return sb.ToString().Trim();
        }
        catch (TaskCanceledException)
        {
            return "COINCIDENCIAS RSMAPS\n⚠ La comparación excedió el tiempo de espera; la alerta se envía de todos modos.";
        }
        catch (HttpRequestException ex)
        {
            return $"COINCIDENCIAS RSMAPS\n⚠ No pude conectar con RSMaps: {Recortar(ex.Message, 160)}";
        }
        catch (Exception ex)
        {
            return $"COINCIDENCIAS RSMAPS\n⚠ Error al comparar: {Recortar(ex.Message, 160)}";
        }
    }

    private static string Recortar(string texto, int max)
    {
        texto = texto.Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= max ? texto : texto[..max] + "…";
    }

    private sealed class MatchingResponse
    {
        public int TotalInventarioEvaluado { get; set; }
        public int TotalCandidatos { get; set; }
        public List<MatchingResultado> Resultados { get; set; } = [];
    }

    private sealed class MatchingResultado
    {
        public int IdInmueble { get; set; }
        public int Puntuacion { get; set; }
        public string Nivel { get; set; } = string.Empty;
        public string? Direccion { get; set; }
        public string? TipoNombre { get; set; }
        public double? Precio { get; set; }
        public int? Recamaras { get; set; }
        public int? BanosCompletos { get; set; }
        public List<string> Coincidencias { get; set; } = [];
        public List<string> Diferencias { get; set; } = [];
    }
}
