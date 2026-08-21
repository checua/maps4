namespace maps4.Repositorios.Contrato
{
    public interface IPublicacionBorradorRepository
    {
        Task PublicarAsync(string correoAutenticado, int idInmueble);
    }
}
