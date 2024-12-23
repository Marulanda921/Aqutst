using MongoDB.Bson;
using MongoDB.Driver;
using TCP_AQUTEST.Infraestructure.Interfaz;

namespace TCP_AQUTEST.Infraestructure.Utils
{
    /// <summary>
    /// Clase que implementa la interfaz IBdService
    /// 
    /// </summary>
    public class BdService : IBdService
    {
        private readonly IMongoDatabase _database;

        public BdService(IMongoDatabase database)
        {
            _database = database;
        }


        /// <summary>
        /// Metodo que inserta un documento en una coleccion de la base de datos
        /// </summary>
        /// <param name="collectionName"></param>
        /// <param name="jsonString"></param>
        /// <returns></returns>
        public async Task InsertDocument(string collectionName, string jsonString)
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var document = BsonDocument.Parse(jsonString);
            await collection.InsertOneAsync(document);
        }
    }
}
