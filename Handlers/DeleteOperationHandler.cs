using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Factories;

namespace StorageSyncWorker.Handlers
{
    internal class DeleteOperationHandler(IEnumerable<IMongoCollection<BsonDocument>> targetCollections, ILogger<OperationHandlersFactory> logger)
        : IOperationHandler
    {
        public async Task HandleAsync(ChangeStreamDocument<BsonDocument> changeStreamDocument, string sourceName)
        {
            var collection = targetCollections
                .FirstOrDefault(x => string.Equals(sourceName, x.CollectionNamespace.CollectionName, StringComparison.OrdinalIgnoreCase));

            if (collection == null)
            {
                return;
            }

            var id = changeStreamDocument.DocumentKey["_id"];

            logger.LogInformation($"Delete detected for document with _id: {id}");

            var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
            await collection.DeleteOneAsync(filter);

            logger.LogInformation("Document deleted from target database.");
        }
    }
}
