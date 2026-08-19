using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInmuebleServicio<T> where T : class
    {
        Task<Inmueble> SaveInmueble(Inmueble modelo);

        Task<bool> UpdateInmueble(Inmueble modelo);

        Task<bool> EliminarInmueble(int idInmueble, string correoAutenticado);

        // Lectura pública: solo PUBLICADO + PUBLICO.
        Task<List<T>> GetInmuebleById(int idInmueble);

        // Lectura privada: valida identidad, Cuenta y propiedad en SQL.
        Task<List<T>> GetInmueblePrivadoById(int idInmueble, string correoAutenticado);
    }
}
