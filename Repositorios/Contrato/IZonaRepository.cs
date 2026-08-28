using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IZonaRepository
    {
        Task<List<ZonaResumenViewModel>> ListarAsync(string correo);
        Task<ZonaEdicionViewModel?> ObtenerAsync(string correo, int idZona);
        Task<ZonaCoberturaActualViewModel?> ObtenerCoberturaActualAsync(string correo);
        Task<int> GuardarAsync(string correo, ZonaGuardarRequest request);
    }
}
