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
    public List<string> TiposPropiedad { get; set; } = [];
    public List<string> Zonas { get; set; } = [];

    public decimal? PrecioMinimo { get; set; }
    public decimal? PrecioMaximo { get; set; }

    public int? RecamarasMin { get; set; }
    public int? RecamarasMax { get; set; }
    public int? BanosMin { get; set; }
    public int? BanosMax { get; set; }

    public decimal? TerrenoMinM2 { get; set; }
    public decimal? ConstruccionMinM2 { get; set; }

    public bool? AceptaMascotas { get; set; }
    public bool? Amueblado { get; set; }
    public bool? UnaPlanta { get; set; }
    public bool? CasetaVigilancia { get; set; }
    public int? CocheraMinAutos { get; set; }

    public List<string> ModalidadesPago { get; set; } = [];
    public string? RequisitosAdicionales { get; set; }

    public double? MejorCoincidencia { get; set; }
    public int? IdInmuebleCoincidente { get; set; }
}
