using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Constants;

namespace StorageSyncWorker.Services
{
    internal class CleanUpService(ILogger<CleanUpService> logger)
        : ICleanUpService
    {
        public async Task RemoveOutdatedRecordsAsync(
            IMongoCollection<BsonDocument> sourceCollection,
            CancellationToken stoppingToken)
        {
            var sourceName = sourceCollection.CollectionNamespace.CollectionName;
            logger.LogInformation("Starting cleanup task for collection '{CollectionName}'", sourceName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Define the outdated record criteria (example: older than 'x' days)
                    var filter = Builders<BsonDocument>.Filter.Lt("sysTime", DateTime.UtcNow.AddDays(-MongoDbOptions.MaxDataAliveInDays));

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        // Find a batch of outdated records
                        var outdatedRecords = await sourceCollection.Find(filter)
                            .Limit(MongoDbOptions.DeleteBunchSize)
                            .ToListAsync(stoppingToken);

                        if (!outdatedRecords.Any())
                        {
                            break;
                        }

                        var idsToDelete = outdatedRecords.Select(doc => doc["_id"]).ToList();
                        var deleteFilter = Builders<BsonDocument>.Filter.In("_id", idsToDelete);

                        var deleteResult = await sourceCollection.DeleteManyAsync(deleteFilter, stoppingToken);

                        logger.LogInformation(
                            "Deleted {DeletedCount} outdated records from collection '{CollectionName}'",
                            deleteResult.DeletedCount,
                            sourceName);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error occurred during cleanup for collection '{CollectionName}'. Retrying in {DelayBetweenDeleteAttemptsInSeconds} seconds...",
                        sourceName,
                        MongoDbOptions.DelayBetweenDeleteAttemptsInSeconds);

                    await Task.Delay(TimeSpan.FromSeconds(MongoDbOptions.DelayBetweenDeleteAttemptsInSeconds), stoppingToken);
                }

                // Delay between cleanup cycles
                await Task.Delay(TimeSpan.FromMinutes(MongoDbOptions.DelayBetweenCleanUpInMinutes), stoppingToken);
            }
        }
    }
}
