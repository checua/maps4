namespace maps4.Models
{
    public class InventarioInmuebleViewModel
    {
        public int IdInmueble { get; set; }
        public int IdCuenta { get; set; }
        public int IdAsesor { get; set; }
        public string? AsesorNombre { get; set; }
        public string? CorreoAsesor { get; set; }
        public string? Direccion { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public int? IdTipo { get; set; }
        public string? TipoNombre { get; set; }
        public string? Telefono { get; set; }
        public double? Terreno { get; set; }
        public double? Construccion { get; set; }
        public double? Precio { get; set; }
        public string? Observaciones { get; set; }
        public int Imagenes { get; set; }
        public string EstadoCodigo { get; set; } = string.Empty;
        public string VisibilidadCodigo { get; set; } = string.Empty;
        public DateTime? FechaPublicacionUtc { get; set; }
        public DateTime? FechaUltimoCambioEstadoUtc { get; set; }

        public bool TienePrecio => Precio.HasValue && Precio.Value > 0;
        public bool TieneFotos => Imagenes > 0;
        public bool TieneUbicacion => Lat.HasValue && Lng.HasValue && Lat.Value != 0 && Lng.Value != 0;
        public bool TieneDescripcionUtil => !string.IsNullOrWhiteSpace(Observaciones) && Observaciones.Trim() != "0";
        public bool TieneDireccionUtil => !string.IsNullOrWhiteSpace(Direccion)
            && !string.Equals(Direccion.Trim(), "Dirección", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Direccion.Trim(), "Direccion", StringComparison.OrdinalIgnoreCase);

        public int PendientesCalidad => new[]
        {
            TienePrecio,
            TieneFotos,
            TieneUbicacion,
            TieneDescripcionUtil
        }.Count(x => !x);

        public bool NecesitaAtencion => PendientesCalidad > 0;
    }

    public class InventarioIndexViewModel
    {
        public string CuentaNombre { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public List<InventarioInmuebleViewModel> Inmuebles { get; set; } = new();
    }
}
