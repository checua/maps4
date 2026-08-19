using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInventarioRepository
    {
        Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor);
    }
}
