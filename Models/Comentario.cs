namespace maps4.Models
{
    public class Comentario
    {
        public int IdComentario { get; set; }
        public string Nombre { get; set; }
        public string Telefono { get; set; }
        public string ComentarioTexto { get; set; }
        public DateTime FechaComentario { get; set; }
        public string Planx { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public bool Activo { get; set; }
    }
}
