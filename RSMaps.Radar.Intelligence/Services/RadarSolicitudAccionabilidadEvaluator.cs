using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class RadarSolicitudAccionabilidadEvaluator
{
    public static RadarSolicitudAccionabilidadDecision Evaluar(
        SolicitudInmobiliaria solicitud,
        RadarSolicitudAccionabilidadPolitica politica,
        bool interpretacionValida = true)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        IReadOnlyList<RadarSolicitudCriterioFuerte> presentes =
            RadarSolicitudCriteriosFuertes.Obtener(solicitud);
        bool contradiccion = TieneDatosContradictorios(solicitud);

        if (contradiccion || !interpretacionValida)
        {
            var motivos = new List<RadarSolicitudAccionabilidadMotivo>();
            if (contradiccion)
                motivos.Add(RadarSolicitudAccionabilidadMotivo.DatosContradictorios);
            if (!interpretacionValida)
                motivos.Add(RadarSolicitudAccionabilidadMotivo.InterpretacionInvalida);

            return Crear(
                RadarSolicitudAccionabilidadEstado.Inconsistente,
                motivos,
                presentes);
        }

        bool tieneOperacion = presentes.Contains(RadarSolicitudCriterioFuerte.Operacion);
        bool tieneTipo = presentes.Contains(RadarSolicitudCriterioFuerte.TipoPropiedad);
        bool cumple = politica switch
        {
            RadarSolicitudAccionabilidadPolitica.Conservadora =>
                tieneOperacion && tieneTipo && presentes.Count >= 3,
            RadarSolicitudAccionabilidadPolitica.Flexible =>
                presentes.Count >= 2 && (tieneOperacion || tieneTipo),
            RadarSolicitudAccionabilidadPolitica.OperacionYTipo =>
                tieneOperacion && tieneTipo,
            _ => throw new ArgumentOutOfRangeException(nameof(politica), politica, null)
        };

        if (cumple)
        {
            return Crear(
                RadarSolicitudAccionabilidadEstado.Accionable,
                [],
                presentes);
        }

        var faltantes = new List<RadarSolicitudAccionabilidadMotivo>();
        if (!tieneOperacion)
            faltantes.Add(RadarSolicitudAccionabilidadMotivo.SinOperacion);
        if (!tieneTipo)
            faltantes.Add(RadarSolicitudAccionabilidadMotivo.SinTipoInmueble);
        if (politica != RadarSolicitudAccionabilidadPolitica.OperacionYTipo)
            faltantes.Add(RadarSolicitudAccionabilidadMotivo.CriteriosInsuficientes);

        return Crear(
            RadarSolicitudAccionabilidadEstado.NecesitaMasDatos,
            faltantes,
            presentes);
    }

    private static bool TieneDatosContradictorios(SolicitudInmobiliaria solicitud) =>
        EsRangoInvalido(solicitud.PrecioMinimo, solicitud.PrecioMaximo) ||
        EsRangoInvalido(solicitud.RecamarasMin, solicitud.RecamarasMax) ||
        EsRangoInvalido(solicitud.BanosMin, solicitud.BanosMax);

    private static bool EsRangoInvalido<T>(T? minimo, T? maximo)
        where T : struct, IComparable<T> =>
        minimo.HasValue &&
        maximo.HasValue &&
        minimo.Value.CompareTo(maximo.Value) > 0;

    private static RadarSolicitudAccionabilidadDecision Crear(
        RadarSolicitudAccionabilidadEstado estado,
        IReadOnlyList<RadarSolicitudAccionabilidadMotivo> motivos,
        IReadOnlyList<RadarSolicitudCriterioFuerte> presentes) =>
        new(estado, motivos, presentes.Count, presentes);
}
