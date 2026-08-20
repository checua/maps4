using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IBorradorInmuebleRepository
    {
        Task<int> CrearAsync(string correoAutenticado, decimal lat, decimal lng, int idTipo);
        Task<BorradorEdicionViewModel?> ObtenerParaEdicionAsync(string correoAutenticado, int idInmueble);
        Task GuardarAsync(string correoAutenticado, BorradorEdicionViewModel modelo);
    }
}
