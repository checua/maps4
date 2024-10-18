using maps4.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace maps4.Repositorios.Contrato
{
    public interface IComentarioService
    {
        Task<List<Comentario>> GetComentariosActivos(int? tipoInmuebleSolicitado);  // Método con parámetro opcional
        Task<bool> RegistrarComentario(Comentario comentario);  // Otro método que debes tener implementado
    }
}