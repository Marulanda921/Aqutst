using MongoDB.Bson;
using MongoDB.Driver;
using TCP_AQUTEST.Services.Contracts.DB;

namespace TCP_AQUTEST.Services.Implementations.DB
{
    public class DBService : IDBService
    {
        private readonly IMongoDatabase _database;

        public DBService(IMongoDatabase database)
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
