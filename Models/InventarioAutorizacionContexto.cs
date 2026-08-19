namespace maps4.Models
{
    public class InventarioAutorizacionContexto
    {
        public int IdCuenta { get; set; }
        public string CuentaNombre { get; set; } = string.Empty;
        public string TipoCuenta { get; set; } = string.Empty;
        public int IdAsesor { get; set; }
        public string RolCodigo { get; set; } = string.Empty;
        public bool PuedeVerCuenta { get; set; }
        public bool PuedeCambiarEstadoCuenta { get; set; }
        public bool PuedeCerrarOperacionCuenta { get; set; }
        public bool PuedeCapturarParaOtro { get; set; }
    }
}
