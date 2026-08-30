using maps4.Models;
using RSMaps.Radar.Listener.Models;
using System.Globalization;
using System.Text;

namespace maps4.Services;

public interface IRadarCentralProcessingService
{
    Task<RadarInterpretationResult> ProcesarAsync(
        string correo,
        RadarMessage mensaje,
        CancellationToken cancellationToken = default);
}

public sealed class RadarCentralProcessingService : IRadarCentralProcessingService
{
    private readonly IRadarCentralIntelligenceService _intelligence;
    private readonly IRadarMatchingService _matching;

    public RadarCentralProcessingService(
        IRadarCentralIntelligenceService intelligence,
        IRadarMatchingService matching)
    {
        _intelligence = intelligence;
        _matching = matching;
    }

    public async Task<RadarInterpretationResult> ProcesarAsync(
        string correo,
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        RadarInterpretationResult interpretacion =
            await _intelligence.InterpretarAsync(mensaje, cancellationToken);

        foreach (SolicitudInmobiliaria solicitud in interpretacion.Solicitudes)
        {
            RadarMatchingResponse resultado = await _matching.CompararAsync(
                correo,
                CrearMatchingRequest(solicitud));

            RadarMatchingResultado? mejor = resultado.Resultados
                .OrderByDescending(x => x.Puntuacion)
                .FirstOrDefault();

            if (mejor is not null)
            {
                solicitud.MejorCoincidencia = mejor.Puntuacion;
                solicitud.IdInmuebleCoincidente = mejor.IdInmueble;
            }

            solicitud.MatchingResumen = ConstruirResumen(resultado);
        }

        return interpretacion;
    }

    private static RadarMatchingRequest CrearMatchingRequest(SolicitudInmobiliaria solicitud)
    {
        return new RadarMatchingRequest
        {
            Operacion = solicitud.Operacion,
            TiposPropiedad = [.. solicitud.TiposPropiedad],
            SubtiposPropiedad = [.. solicitud.SubtiposPropiedad],
            Zonas = [.. solicitud.Zonas],
            CondicionInmueble = solicitud.CondicionInmueble,
            EtapaInmueble = solicitud.EtapaInmueble,
            PrecioMinimo = solicitud.PrecioMinimo,
            PrecioMaximo = solicitud.PrecioMaximo,
            RecamarasMin = solicitud.RecamarasMin,
            RecamarasMax = solicitud.RecamarasMax,
            BanosMin = solicitud.BanosMin,
            BanosMax = solicitud.BanosMax,
            TerrenoMinM2 = solicitud.TerrenoMinM2,
            ConstruccionMinM2 = solicitud.ConstruccionMinM2,
            AceptaMascotas = solicitud.AceptaMascotas,
            Amueblado = solicitud.Amueblado,
            UnaPlanta = solicitud.UnaPlanta,
            CasetaVigilancia = solicitud.CasetaVigilancia,
            CocheraMinAutos = solicitud.CocheraMinAutos,
            MaxResultados = 5
        };
    }

    private static string ConstruirResumen(RadarMatchingResponse resultado)
    {
        if (resultado.TotalCandidatos <= 0 || resultado.Resultados.Count == 0)
        {
            return $"COINCIDENCIAS RSMAPS\n❌ Sin coincidencias útiles actuales.\n" +
                   $"Inventario evaluado: {resultado.TotalInventarioEvaluado}.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("COINCIDENCIAS RSMAPS");

        foreach (RadarMatchingResultado item in resultado.Resultados.Take(3))
        {
            string icono = item.Puntuacion >= 85 ? "🟢" : item.Puntuacion >= 70 ? "🟡" : "⚪";
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

            foreach (string motivo in item.Coincidencias
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Take(3))
            {
                sb.AppendLine($"✓ {motivo}");
            }
        }

        sb.Append($"Candidatos: {resultado.TotalCandidatos} de {resultado.TotalInventarioEvaluado} evaluados.");
        return sb.ToString().Trim();
    }
}
