namespace RSMaps.Radar.Listener.Models;

public sealed class RadarKnowledgeDocument
{
    public int Version { get; set; } = 1;
    public List<RadarKnowledgeTerm> Terminos { get; set; } = [];
    public List<RadarTrainingExample> Ejemplos { get; set; } = [];
}

public sealed class RadarKnowledgeTerm
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Termino { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string? ValorCanonico { get; set; }
    public List<string> Alias { get; set; } = [];
    public string? Instruccion { get; set; }
    public string Ambito { get; set; } = "Global";
    public string? Cuenta { get; set; }
    public string? Usuario { get; set; }
    public bool Activo { get; set; } = true;
    public bool ConfirmadoPorHumano { get; set; } = true;
}

public sealed class RadarTrainingExample
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Mensaje { get; set; } = "";
    public List<string> Activadores { get; set; } = [];
    public string InterpretacionCorrecta { get; set; } = "";
    public string Ambito { get; set; } = "Global";
    public string? Cuenta { get; set; }
    public string? Usuario { get; set; }
    public bool Activo { get; set; } = true;
    public bool ConfirmadoPorHumano { get; set; } = true;
}
