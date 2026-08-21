using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IMarketplaceFiltroRepository
    {
        Task<List<AmenidadFiltroViewModel>> ListarAmenidadesAsync();
    }
}
