using System.ComponentModel.DataAnnotations;

namespace maps4.Models
{
    public class BorradorEdicionViewModel
    {
        public int IdInmueble { get; set; }
        public int IdCuenta { get; set; }
        public int IdAsesor { get; set; }

        [Display(Name = "Dirección o referencia")]
        public string? Direccion { get; set; }

        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }

        [Required(ErrorMessage = "Selecciona el tipo de propiedad.")]
        [Display(Name = "Tipo de propiedad")]
        public int IdTipo { get; set; }
        public string? TipoNombre { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El terreno no puede ser negativo.")]
        [Display(Name = "Terreno (m²)")]
        public double? Terreno { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "La construcción no puede ser negativa.")]
        [Display(Name = "Construcción (m²)")]
        public double? Construccion { get; set; }

        [Range(0, 9999999999999999d, ErrorMessage = "El precio no puede ser negativo.")]
        [Display(Name = "Precio")]
        public decimal? Precio { get; set; }

        [Range(0, 100, ErrorMessage = "Las recámaras deben estar entre 0 y 100.")]
        [Display(Name = "Recámaras")]
        public int? Recamaras { get; set; }

        [Range(0, 100, ErrorMessage = "Los baños completos deben estar entre 0 y 100.")]
        [Display(Name = "Baños completos")]
        public int? BanosCompletos { get; set; }

        [Range(0, 100, ErrorMessage = "Los medios baños deben estar entre 0 y 100.")]
        [Display(Name = "Medios baños")]
        public int? MediosBanos { get; set; }

        [Range(0, 100, ErrorMessage = "Los estacionamientos deben estar entre 0 y 100.")]
        [Display(Name = "Estacionamientos")]
        public int? Estacionamientos { get; set; }

        [Range(0, 100, ErrorMessage = "Los niveles deben estar entre 0 y 100.")]
        [Display(Name = "Niveles")]
        public int? Niveles { get; set; }

        [Range(0, 500, ErrorMessage = "La antigüedad debe estar entre 0 y 500 años.")]
        [Display(Name = "Antigüedad (años)")]
        public int? AntiguedadAnos { get; set; }

        [Display(Name = "Descripción")]
        public string? Observaciones { get; set; }

        [Display(Name = "Notas privadas")]
        public string? NotasPrivadas { get; set; }

        public List<string> AmenidadesSeleccionadas { get; set; } = new();
        public List<AmenidadOpcionViewModel> AmenidadesDisponibles { get; set; } = new();

        public int Imagenes { get; set; }
        public List<InmuebleFotoViewModel> Fotos { get; set; } = new();
        public string EstadoCodigo { get; set; } = "BORRADOR";
        public string VisibilidadCodigo { get; set; } = "CUENTA";
        public DateTime? FechaUltimaEdicionUtc { get; set; }
        public List<TipoPropiedad> TiposDisponibles { get; set; } = new();

        public bool TieneUbicacion => Lat.HasValue && Lng.HasValue && Lat.Value != 0 && Lng.Value != 0;
        public bool TieneTipo => IdTipo > 1;
        public bool TienePrecio => Precio.HasValue && Precio.Value > 0;
        public bool TieneSuperficie => (Terreno.HasValue && Terreno.Value > 0) || (Construccion.HasValue && Construccion.Value > 0);
        public bool TieneDescripcion => !string.IsNullOrWhiteSpace(Observaciones);
        public bool TieneFotos => Imagenes > 0 || Fotos.Count > 0;
        public int PasosCompletos => new[] { TieneUbicacion, TieneTipo, TienePrecio, TieneSuperficie, TieneDescripcion, TieneFotos }.Count(x => x);
        public int ProgresoPorcentaje => (int)Math.Round(PasosCompletos / 6.0 * 100);
    }
}
