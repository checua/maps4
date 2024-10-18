namespace maps4.Models
{
    public class Comentario
    {
        public int IdComentario { get; set; }
        public string Correo { get; set; }
        public string Nombre { get; set; }
        public string Telefono { get; set; }
        public string ComentarioTexto { get; set; }
        public DateTime FechaComentario { get; set; }
        public string Nivel { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public bool Activo { get; set; }
        public int? TipoInmuebleSolicitado { get; set; } // Nuevo campo para el tipo de inmueble
    }
}
