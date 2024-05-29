namespace maps4.Repositorios.Contrato
{
    public interface IUsuarioService<T> where T : class
    {
        Task<List<T>> Lista(String correo, String contra);
    }
}
