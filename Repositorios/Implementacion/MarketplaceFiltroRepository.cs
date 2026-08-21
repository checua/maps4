using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.Data.SqlClient;

namespace maps4.Repositorios.Implementacion
{
    public class MarketplaceFiltroRepository : IMarketplaceFiltroRepository
    {
        private readonly string _cadenaSQL;

        public MarketplaceFiltroRepository(IConfiguration configuration)
        {
            _cadenaSQL = configuration.GetConnectionString("cadenaSQL") ?? string.Empty;
        }

        public async Task<List<AmenidadFiltroViewModel>> ListarAmenidadesAsync()
        {
            List<AmenidadFiltroViewModel> lista = new();
            using SqlConnection conexion = new(_cadenaSQL);
            await conexion.OpenAsync();

            const string sql = @"
SELECT Codigo, Nombre, Grupo, Orden
FROM dbo.RSMAPS_Amenidad
WHERE Activo = 1 AND EsFiltro = 1
ORDER BY Grupo, Orden, Nombre;";

            using SqlCommand cmd = new(sql, conexion);
            using SqlDataReader dr = await cmd.ExecuteReaderAsync();
            while (await dr.ReadAsync())
            {
                lista.Add(new AmenidadFiltroViewModel
                {
                    Codigo = dr["Codigo"].ToString() ?? string.Empty,
                    Nombre = dr["Nombre"].ToString() ?? string.Empty,
                    Grupo = dr["Grupo"].ToString() ?? string.Empty,
                    Orden = Convert.ToInt32(dr["Orden"])
                });
            }

            return lista;
        }
    }
}
