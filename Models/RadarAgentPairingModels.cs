namespace maps4.Models;

public sealed class RadarAgentPairingPageViewModel
{
    public string NombreAgent { get; set; } = "";
    public string? CuentaNombre { get; set; }
    public string? Codigo { get; set; }
    public DateTime? ExpiraUtc { get; set; }
    public List<RadarAgentDeviceListItem> Agents { get; set; } = [];
}

public sealed class RadarAgentDeviceListItem
{
    public Guid IdAgent { get; set; }
    public string NombreAgent { get; set; } = "";
    public string? EquipoNombre { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaAltaUtc { get; set; }
    public DateTime? UltimoUsoUtc { get; set; }
    public DateTime? RevocadoUtc { get; set; }
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

public sealed class RadarAgentAuthenticationResult
{
    public Guid IdAgent { get; set; }
    public string NombreAgent { get; set; } = "";
    public string? EquipoNombre { get; set; }
    public int IdAsesor { get; set; }
    public int IdCuenta { get; set; }
    public string CuentaNombre { get; set; } = "";
    public string RolCodigo { get; set; } = "";
    public string Correo { get; set; } = "";
}
