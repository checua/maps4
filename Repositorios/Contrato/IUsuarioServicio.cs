using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IUsuarioServicio<T> where T : class
    {
        Task<List<T>> GetUsuario(String correo, String contra);

        Task<Usuario> SaveUsuario(Usuario modelo);
    }
}
