namespace maps4.Models
{
    public class Inmueble
    {
        public int IdInmueble { get; set; }
        public Inmobiliaria? refInmobiliaria { get; set; } //Referencia al Modelo Inmobiliaria
        public Usuario? refUsuario { get; set; } //Referencia al Modelo Usuario
        public string Direccion { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public int? IdTipo { get; set; }
        public string Telefono { get; set; }
        public string? Terreno { get; set; }
        public string? Construccion { get; set; }
        public string? Precio { get; set; }
        public string? Observaciones { get; set; }
        public int? Exclusiva { get; set; }
        public string Link { get; set; }
    }
}
