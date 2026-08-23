namespace maps4.Models;

public sealed class RadarInmuebleDto
{
    public int IdInmueble { get; init; }
    public int? IdTipo { get; init; }
    public string? Tipo { get; init; }
    public string? Direccion { get; init; }
    public decimal? Lat { get; init; }
    public decimal? Lng { get; init; }
    public decimal? Precio { get; init; }
    public decimal? TerrenoM2 { get; init; }
    public decimal? ConstruccionM2 { get; init; }
    public int? Recamaras { get; init; }
    public int? BanosCompletos { get; init; }
    public int? MediosBanos { get; init; }
    public int? Estacionamientos { get; init; }
    public int? Niveles { get; init; }
    public string? Amenidades { get; init; }
    public string? Observaciones { get; init; }
    public string? Link { get; init; }
    public int Imagenes { get; init; }
}
