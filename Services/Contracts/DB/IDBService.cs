namespace TCP_AQUTEST.Services.Contracts.DB
{
    public interface IDBService
    {

        //Interfaz de la base de datos, recibe las colecciones y el string y las inserta a una base de datos
        Task InsertDocument(string collectionName, string jsonString);
    }
}
