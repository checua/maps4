using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInmuebleServicio<T> where T : class
    {
        //Task<List<T>> GetInmuebles();

        Task<Inmueble> SaveInmueble(Inmueble modelo);//, int files, string correo);

        Task<bool> EliminarInmueble(int idInmueble);
    }
}
