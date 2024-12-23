namespace TCP_AQUTEST.Infraestructure.Interfaz
{
    /// <summary>
    ///Interfaz de la base de datos, recibe las colecciones y el string 
    ///las inserta a una base de datos
    /// </summary>
    public interface IBdService
    {Task InsertDocument(string collectionName, string jsonString);}
}
