using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IInmuebleFotoStorage
    {
        Task<FotoAlmacenada> GuardarAsync(int idInmueble, IFormFile archivo, CancellationToken cancellationToken = default);
        Task<Stream?> AbrirLecturaAsync(string claveAlmacenamiento, CancellationToken cancellationToken = default);
        Task EliminarAsync(string claveAlmacenamiento, CancellationToken cancellationToken = default);
    }
}
