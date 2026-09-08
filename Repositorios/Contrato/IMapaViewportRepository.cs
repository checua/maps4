using maps4.Models;

namespace maps4.Repositorios.Contrato
{
    public sealed record MapaViewportResultado(IReadOnlyList<Inmueble> Items, bool Truncated);

    public interface IMapaViewportRepository
    {
        Task<MapaViewportResultado> ListarAsync(
            decimal north,
            decimal south,
            decimal east,
            decimal west,
            int maxResultados,
            CancellationToken cancellationToken = default);
    }
}
