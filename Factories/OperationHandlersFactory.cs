using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Handlers;
using StorageSyncWorker.Models;

namespace StorageSyncWorker.Factories
{
    internal class OperationHandlersFactory : IOperationHandlersFactory
    {
        private readonly ILogger<OperationHandlersFactory> _logger;
        private readonly IReadOnlyList<IMongoCollection<BsonDocument>> _targetCollections;

        public OperationHandlersFactory(ILogger<OperationHandlersFactory> logger, IOptions<StorageOptions> storageOptions)
        {
            _logger = logger;

            var storageOptionsValue = storageOptions.Value;
            var targetClient = new MongoClient(storageOptionsValue.TargetDatabaseConnection);

            _targetCollections = storageOptionsValue.CollectionNames
                .Select(name => targetClient.GetDatabase(storageOptionsValue.TargetDatabase).GetCollection<BsonDocument>(name))
                .ToList();
        }

        public IOperationHandler GetHandler(ChangeStreamOperationType streamOperationType)
        {
            return streamOperationType switch
            {
                ChangeStreamOperationType.Insert => new InsertOperationHandler(_targetCollections, _logger),
                ChangeStreamOperationType.Update => new UpdateOperationHandler(_targetCollections, _logger),
                ChangeStreamOperationType.Delete => new DeleteOperationHandler(_targetCollections, _logger),
                _ => null
            };
        }
    }
}
