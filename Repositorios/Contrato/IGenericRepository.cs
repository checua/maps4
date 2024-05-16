using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public interface IGenericRepository<T> where T : class
    {
        Task<List<T>> Lista();
        Task<bool> Guardar(T modelo);
        Task<bool> Editar(T modelo);
        Task<bool> Eliminar(int id);
    }

    public interface IUsuarioService
    {
        Task<Usuario> GetUsuario(string correo, string clave);
        Task<Usuario> SaveUsuario(Usuario modelo);

    }
}
