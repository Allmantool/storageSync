using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Constants;
using StorageSyncWorker.Factories;

namespace StorageSyncWorker.Handlers
{
    internal class UpdateOperationHandler(IEnumerable<IMongoCollection<BsonDocument>> targetCollections, ILogger<OperationHandlersFactory> logger)
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

            var id = changeStreamDocument.DocumentKey[FieldNames.Id];
            var updatedFields = changeStreamDocument.UpdateDescription.UpdatedFields.Elements;

            logger.LogInformation($"Update detected for document with _id: {id}");

            var filter = Builders<BsonDocument>.Filter.Eq(FieldNames.Id, id);
            var update = Builders<BsonDocument>.Update.Combine(
                updatedFields.Select(e => Builders<BsonDocument>.Update.Set(e.Name, e.Value))
            );

            await collection.UpdateOneAsync(filter, update);

            logger.LogInformation("Document updated in target database.");
        }
    }
}
