namespace maps4.Models;

public sealed class RadarAgentPairingPageViewModel
{
    public string NombreAgent { get; set; } = "";
    public string? CuentaNombre { get; set; }
    public string? Codigo { get; set; }
    public DateTime? ExpiraUtc { get; set; }
}

public sealed class RadarAgentPairingCreateResult
{
    public string Codigo { get; set; } = "";
    public DateTime ExpiraUtc { get; set; }
    public int IdAsesor { get; set; }
    public int IdCuenta { get; set; }
    public string CuentaNombre { get; set; } = "";
}

public sealed class RadarAgentPairingExchangeRequest
{
    public string Codigo { get; set; } = "";
    public string NombreAgent { get; set; } = "";
    public string? EquipoNombre { get; set; }
}

public sealed class RadarAgentPairingExchangeResult
{
    public Guid IdAgent { get; set; }
    public string Token { get; set; } = "";
    public int IdAsesor { get; set; }
    public int IdCuenta { get; set; }
    public string CuentaNombre { get; set; } = "";
    public string RolCodigo { get; set; } = "";
}
