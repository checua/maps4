using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInmuebleServicio<T> where T : class
    {
        Task<Inmueble> SaveInmueble(Inmueble modelo);

        Task<bool> UpdateInmueble(Inmueble modelo);

        Task<bool> EliminarInmueble(int idInmueble, string correoAutenticado);

        Task<List<T>> GetInmuebleById(int idInmueble);
    }
}
