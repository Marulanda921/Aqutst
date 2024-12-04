using MongoDB.Bson;
using MongoDB.Driver;
using TCP_AQUTEST.Infraestructure.Interfaz;

namespace TCP_AQUTEST.Infraestructure.Utils
{
    public class BdService : IBdService
    {
        private readonly IMongoDatabase _database;

        public BdService(IMongoDatabase database)
        {
            _database = database;
        }

        public async Task InsertDocument(string collectionName, string jsonString)
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var document = BsonDocument.Parse(jsonString);
            await collection.InsertOneAsync(document);
        }
    }
}
