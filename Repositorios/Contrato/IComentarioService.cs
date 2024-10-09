using maps4.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace maps4.Repositorios.Contrato
{
    public interface IComentarioService
    {
        // Método para obtener los comentarios activos (los que aún no han expirado)
        Task<List<Comentario>> GetComentariosActivos();

        // Método para registrar un nuevo comentario
        Task<bool> RegistrarComentario(Comentario comentario);
    }
}
