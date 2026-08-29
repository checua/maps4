namespace maps4.Models
{
    public class RadarMatchingRequest
    {
        public string? Operacion { get; set; }
        public List<string> TiposPropiedad { get; set; } = new();
        public List<string> SubtiposPropiedad { get; set; } = new();
        public List<string> Zonas { get; set; } = new();

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

        public int MaxResultados { get; set; } = 5;
    }

    public class RadarMatchingResultado
    {
        public int IdInmueble { get; set; }
        public int Puntuacion { get; set; }
        public string Nivel { get; set; } = string.Empty;

        public string? Direccion { get; set; }
        public string? TipoNombre { get; set; }
        public double? Precio { get; set; }
        public int? Recamaras { get; set; }
        public int? BanosCompletos { get; set; }
        public int? Estacionamientos { get; set; }
        public double? Terreno { get; set; }
        public double? Construccion { get; set; }
        public string? Link { get; set; }

        public List<string> Coincidencias { get; set; } = new();
        public List<string> Diferencias { get; set; } = new();
    }

    public class RadarMatchingResponse
    {
        public int TotalInventarioEvaluado { get; set; }
        public int TotalCandidatos { get; set; }
        public List<RadarMatchingResultado> Resultados { get; set; } = new();
    }
}
