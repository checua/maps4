using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInventarioRepository
    {
        Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor);

        Task CambiarEstadoOVisibilidadAsync(
            int idInmueble,
            string correo,
            string? estadoNuevo,
            string? visibilidadNueva,
            string? motivo);

        Task CerrarOperacionAsync(
            int idInmueble,
            string correo,
            string tipoOperacion,
            decimal precioCierre,
            DateTime fechaCierreUtc,
            string? notasCierre);
    }
}
