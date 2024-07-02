namespace maps4.Models
{
    public class Inmueble
    {
        public int IdInmueble { get; set; }
        public Inmobiliaria? refInmobiliaria { get; set; } //Referencia al Modelo Inmobiliaria
        public Usuario? refUsuario { get; set; } //Referencia al Modelo Usuario
        public string? Direccion { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public int? IdTipo { get; set; }
        public string Telefono { get; set; }
        public float? Terreno { get; set; }
        public float? Construccion { get; set; }
        public float? Precio { get; set; }
        public string? Observaciones { get; set; }
        public int? Exclusiva { get; set; }
        public string? Link { get; set; }
        public string? Contacto { get; set; }
        public int Imagenes { get; set; }
    }
}
