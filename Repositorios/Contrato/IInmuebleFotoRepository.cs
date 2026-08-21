using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInmuebleFotoRepository
    {
        Task<List<InmuebleFotoViewModel>> ListarAsync(string correoAutenticado, int idInmueble);
        Task<InmuebleFotoViewModel?> ObtenerAsync(string correoAutenticado, long idImagen);
        Task<InmuebleFotoViewModel?> ObtenerPublicaPorOrdenAsync(int idInmueble, int orden);
        Task<long> RegistrarAsync(string correoAutenticado, int idInmueble, FotoAlmacenada foto);
        Task EstablecerPortadaAsync(string correoAutenticado, int idInmueble, long idImagen);
        Task EliminarMetadataAsync(string correoAutenticado, int idInmueble, long idImagen);
        Task MoverAsync(string correoAutenticado, int idInmueble, long idImagen, int direccion);
    }
}
