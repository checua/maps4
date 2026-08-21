namespace maps4.Models
{
    public class AmenidadOpcionViewModel
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Seleccionada { get; set; }
    }
}
