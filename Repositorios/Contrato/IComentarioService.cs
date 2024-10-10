using maps4.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace maps4.Repositorios.Contrato
{
    public interface IComentarioService
    {
        /// <summary>
        /// Registra un nuevo comentario en la base de datos.
        /// </summary>
        /// <param name="comentario">Comentario a registrar</param>
        /// <returns>True si el comentario se registró correctamente, False de lo contrario</returns>
        Task<bool> RegistrarComentario(Comentario comentario);

        /// <summary>
        /// Obtiene todos los comentarios activos de la base de datos.
        /// </summary>
        /// <returns>Lista de comentarios activos</returns>
        Task<List<Comentario>> GetComentariosActivos();
    }
}