namespace RSMaps.Radar.Listener.Models;

public class SolicitudInmobiliaria
{
    public string ChatOrigen { get; set; } = "";
    public string? Autor { get; set; }
    public string? Telefono { get; set; }

    public string MessageId { get; set; } = "";
    public string MensajeOriginal { get; set; } = "";
    public DateTime DetectadoEn { get; set; }

    public string? Operacion { get; set; }
    public string? TipoPropiedad { get; set; }
    public string? Zona { get; set; }

    public decimal? PrecioMinimo { get; set; }
    public decimal? PrecioMaximo { get; set; }

    public int? Recamaras { get; set; }
    public int? Banos { get; set; }

    public decimal? TerrenoM2 { get; set; }
    public decimal? ConstruccionM2 { get; set; }

    public bool? AceptaMascotas { get; set; }
    public bool? Amueblado { get; set; }

    public double? MejorCoincidencia { get; set; }
    public int? IdInmuebleCoincidente { get; set; }
}
