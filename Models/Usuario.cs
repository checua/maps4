namespace maps4.Models
{
    public class Usuario
    {
        public int idAsesor { get; set; }
        public string? nombres { get; set; }
        public string? aPaterno { get; set; }
        public string? aMaterno { get; set; }
        public Inmobiliaria? refnmobiliaria { get; set; } //Referencia al Modelo Inmobiliaria
        public string? nick { get; set; }
        public string? contra { get; set; }
        public string? telefono { get; set; }
        public string? correo { get; set; }
        public string? foto { get; set; }
        public string? obs { get; set; }
        public string? dob { get; set; }
        public string? revisado { get; set; }
    }
}
