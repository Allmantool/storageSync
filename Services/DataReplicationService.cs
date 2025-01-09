using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Constants;
using StorageSyncWorker.Factories;

namespace StorageSyncWorker.Services
{
    internal class DataReplicationService(
        ILogger<DataReplicationService> logger,
        IOperationHandlersFactory operationHandlersFactory)
        : IDataReplicationService
    {
        public async Task WatchAndSyncChangeStreamAsync(
            IMongoCollection<BsonDocument> sourceCollection,
            CancellationToken stoppingToken)
        {
            var sourceName = sourceCollection.CollectionNamespace.CollectionName;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var changeStream = await sourceCollection.WatchAsync(cancellationToken: stoppingToken);
                    await changeStream.ForEachAsync(async changeStreamDocument =>
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            return;
                        }

                        var handler = operationHandlersFactory.GetHandler(changeStreamDocument.OperationType);

                        if (handler == null)
                        {
                            logger.LogWarning(
                                "No handler found for operation type {OperationType} in collection {CollectionName}",
                                changeStreamDocument.OperationType,
                                sourceName);

                            return;
                        }

                        try
                        {
                            await handler.HandleAsync(changeStreamDocument, sourceName);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error handling change for collection {CollectionName}", sourceName);
                        }
                    }, stoppingToken);
                }
                catch (MongoException ex) when (ex is MongoConnectionException or MongoExecutionTimeoutException)
                {
                    logger.LogError(
                        ex,
                        "MongoDB connection issue detected for collection '{CollectionName}'. Retrying in {DelayBetweenSyncAttemptsInSeconds} seconds...",
                        sourceName,
                        MongoDbOptions.DelayBetweenSyncAttemptsInSeconds);

                    await Task.Delay(TimeSpan.FromSeconds(MongoDbOptions.DelayBetweenSyncAttemptsInSeconds), stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Unexpected error occurred while watching collection '{CollectionName}'. Retrying in {DelayBetweenSyncAttemptsInSeconds} seconds...",
                        sourceName,
                        MongoDbOptions.DelayBetweenSyncAttemptsInSeconds);

                    await Task.Delay(TimeSpan.FromSeconds(MongoDbOptions.DelayBetweenSyncAttemptsInSeconds), stoppingToken);
                }
            }
        }
    }
}
