using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInmuebleServicio<T> where T : class
    {
        Task<List<T>> GetInmuebles();

        Task<Data> SaveInmueble(Data datax);//, int files, string correo);
    }
}
