using System.ComponentModel.DataAnnotations;

namespace maps4.Models
{
    public class CerrarOperacionViewModel
    {
        public int IdInmueble { get; set; }

        [Required(ErrorMessage = "Selecciona si la operación fue venta o renta.")]
        public string TipoOperacion { get; set; } = "VENTA";

        [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "El precio de cierre debe ser mayor que cero.")]
        [Display(Name = "Precio de cierre")]
        public decimal PrecioCierre { get; set; }

        [Required(ErrorMessage = "Indica la fecha de cierre.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de cierre")]
        public DateOnly FechaCierre { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [StringLength(1000, ErrorMessage = "Las notas no pueden exceder 1000 caracteres.")]
        [Display(Name = "Notas de cierre")]
        public string? NotasCierre { get; set; }

        // Datos de solo lectura para presentar contexto antes de confirmar.
        public string? TipoNombre { get; set; }
        public string? Direccion { get; set; }
        public double? PrecioPublicado { get; set; }
        public string EstadoActual { get; set; } = string.Empty;
        public string VisibilidadActual { get; set; } = string.Empty;
        public int Imagenes { get; set; }
    }
}
