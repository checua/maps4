namespace RSMaps.Radar.Listener.Models;

public enum RadarSolicitudAccionabilidadEstado
{
    Accionable,
    NecesitaMasDatos,
    Inconsistente
}

public enum RadarSolicitudAccionabilidadMotivo
{
    SinOperacion,
    SinTipoInmueble,
    CriteriosInsuficientes,
    DatosContradictorios,
    InterpretacionInvalida
}

public enum RadarSolicitudAccionabilidadPolitica
{
    Conservadora,
    Flexible,
    OperacionYTipo
}

public enum RadarSolicitudCriterioFuerte
{
    Operacion,
    TipoPropiedad,
    SubtipoPropiedad,
    Zona,
    TipoFraccionamiento,
    Precio,
    Recamaras,
    Banos,
    Superficie,
    Cochera,
    Caracteristicas,
    ModalidadPago
}

public sealed record RadarSolicitudAccionabilidadDecision(
    RadarSolicitudAccionabilidadEstado Estado,
    IReadOnlyList<RadarSolicitudAccionabilidadMotivo> Motivos,
    int CriteriosFuertes,
    IReadOnlyList<RadarSolicitudCriterioFuerte> CriteriosPresentes);
