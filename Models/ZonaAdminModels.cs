namespace maps4.Models
{
    public class ZonaAdminIndexViewModel
    {
        public List<ZonaResumenViewModel> Zonas { get; set; } = new();
        public List<ZonaInmueblePinViewModel> Inmuebles { get; set; } = new();
    }

    public class ZonaResumenViewModel
    {
        public int IdZona { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Prioridad { get; set; }
        public string? ColorHex { get; set; }
        public bool Activa { get; set; }
        public int Poligonos { get; set; }
        public int Alias { get; set; }
        public int Inmuebles { get; set; }
        public int Principales { get; set; }
    }

    public class ZonaEdicionViewModel
    {
        public int IdZona { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Prioridad { get; set; }
        public string? ColorHex { get; set; }
        public bool Activa { get; set; }
        public List<string> Aliases { get; set; } = new();
        public List<ZonaVerticeViewModel> Vertices { get; set; } = new();
    }

    public class ZonaGuardarRequest
    {
        public int? IdZona { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Prioridad { get; set; } = 100;
        public string? ColorHex { get; set; } = "#ef4444";
        public List<string> Aliases { get; set; } = new();
        public List<ZonaVerticeViewModel> Vertices { get; set; } = new();
    }

    public class ZonaVerticeViewModel
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class ZonaInmueblePinViewModel
    {
        public int IdInmueble { get; set; }
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
        public string? Tipo { get; set; }
        public string? Direccion { get; set; }
        public double? Precio { get; set; }
    }
}
