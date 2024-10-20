using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Factories;

namespace StorageSyncWorker.Handlers
{
    internal class InsertOperationHandler(IEnumerable<IMongoCollection<BsonDocument>> targetCollections, ILogger<OperationHandlersFactory> logger)
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

            var document = changeStreamDocument.FullDocument;

            logger.LogInformation("Insert detected: " + document);

            await collection.InsertOneAsync(document);

            logger.LogInformation($"Document inserted in target database.{sourceName}");
        }
    }
}
