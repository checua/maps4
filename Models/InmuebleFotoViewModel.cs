namespace maps4.Models
{
    public class InmuebleFotoViewModel
    {
        public long IdImagen { get; set; }
        public int IdInmueble { get; set; }
        public string ClaveAlmacenamiento { get; set; } = string.Empty;
        public string? NombreOriginal { get; set; }
        public string MimeType { get; set; } = "image/jpeg";
        public long Bytes { get; set; }
        public int Orden { get; set; }
        public bool EsPortada { get; set; }
        public DateTime FechaAltaUtc { get; set; }

        public string UrlPrivada => $"/Borrador/Foto/{IdImagen}";
    }

    public class FotoAlmacenada
    {
        public string ClaveAlmacenamiento { get; set; } = string.Empty;
        public string NombreOriginal { get; set; } = string.Empty;
        public string MimeType { get; set; } = "image/jpeg";
        public long Bytes { get; set; }
    }
}
