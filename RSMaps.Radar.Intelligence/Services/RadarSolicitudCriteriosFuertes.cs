using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public static class RadarSolicitudCriteriosFuertes
{
    public static IReadOnlyList<RadarSolicitudCriterioFuerte> Obtener(
        SolicitudInmobiliaria solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var criterios = new List<RadarSolicitudCriterioFuerte>();

        Agregar(!string.IsNullOrWhiteSpace(solicitud.Operacion), RadarSolicitudCriterioFuerte.Operacion);
        Agregar(solicitud.TiposPropiedad.Count > 0, RadarSolicitudCriterioFuerte.TipoPropiedad);
        Agregar(solicitud.SubtiposPropiedad.Count > 0, RadarSolicitudCriterioFuerte.SubtipoPropiedad);
        Agregar(solicitud.Zonas.Count > 0, RadarSolicitudCriterioFuerte.Zona);
        Agregar(!string.IsNullOrWhiteSpace(solicitud.TipoFraccionamiento), RadarSolicitudCriterioFuerte.TipoFraccionamiento);
        Agregar(solicitud.PrecioMinimo.HasValue || solicitud.PrecioMaximo.HasValue, RadarSolicitudCriterioFuerte.Precio);
        Agregar(solicitud.RecamarasMin.HasValue || solicitud.RecamarasMax.HasValue, RadarSolicitudCriterioFuerte.Recamaras);
        Agregar(solicitud.BanosMin.HasValue || solicitud.BanosMax.HasValue, RadarSolicitudCriterioFuerte.Banos);
        Agregar(solicitud.TerrenoMinM2.HasValue || solicitud.ConstruccionMinM2.HasValue, RadarSolicitudCriterioFuerte.Superficie);
        Agregar(solicitud.CocheraMinAutos.HasValue, RadarSolicitudCriterioFuerte.Cochera);
        Agregar(
            solicitud.AceptaMascotas.HasValue ||
            solicitud.Amueblado.HasValue ||
            solicitud.UnaPlanta.HasValue ||
            solicitud.CasetaVigilancia.HasValue,
            RadarSolicitudCriterioFuerte.Caracteristicas);
        Agregar(solicitud.ModalidadesPago.Count > 0, RadarSolicitudCriterioFuerte.ModalidadPago);

        return criterios;

        void Agregar(bool presente, RadarSolicitudCriterioFuerte criterio)
        {
            if (presente)
                criterios.Add(criterio);
        }
    }

    public static int Contar(SolicitudInmobiliaria solicitud) =>
        Obtener(solicitud).Count;
}
