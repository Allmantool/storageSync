using MongoDB.Bson;
using MongoDB.Driver;

namespace StorageSyncWorker.Services
{
    public interface IDataReplicationService
    {
        Task WatchAndSyncChangeStreamAsync(
           IMongoCollection<BsonDocument> sourceCollection,
           CancellationToken stoppingToken);
    }
}
