namespace maps4.Repositorios.Contrato
{
    public interface IBorradorInmuebleRepository
    {
        Task<int> CrearAsync(string correoAutenticado, decimal lat, decimal lng, int idTipo);
    }
}
