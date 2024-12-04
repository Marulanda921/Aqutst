namespace TCP_AQUTEST.Infraestructure.Interfaz
{
    public interface IBdService
    {
        Task InsertDocument(string collectionName, string jsonString);
    }
}
