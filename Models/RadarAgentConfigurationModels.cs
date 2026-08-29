namespace maps4.Models;

public sealed class RadarAgentConfiguration
{
    public Guid IdAgent { get; set; }
    public string NombreAgent { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public bool Activo { get; set; }
    public DateTime? RevocadoUtc { get; set; }
    public bool Configurada { get; set; }
    public List<string> ChatsMonitoreados { get; set; } = [];
    public string? DestinoAlertas { get; set; }
    public int IntervaloRevisionMs { get; set; } = 60_000;
    public Dictionary<string, string[]> TerminosBusqueda { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public DateTime? ActualizadoUtc { get; set; }
}

public sealed class RadarAgentConfigurationEditViewModel
{
    public Guid IdAgent { get; set; }
    public string NombreAgent { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public bool Activo { get; set; }
    public bool Configurada { get; set; }
    public string ChatsTexto { get; set; } = string.Empty;
    public List<string> ChatsSeleccionados { get; set; } = [];
    public List<RadarAgentDiscoveredChatItem> ChatsDisponibles { get; set; } = [];
    public DateTime? ChatsDetectadosUtc { get; set; }
    public string DestinoAlertas { get; set; } = "Propiedades";
    public int IntervaloRevisionMs { get; set; } = 60_000;
    public DateTime? ActualizadoUtc { get; set; }
}
