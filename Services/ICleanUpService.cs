using MongoDB.Bson;
using MongoDB.Driver;

namespace StorageSyncWorker.Services
{
    public interface ICleanUpService
    {
        Task RemoveOutdatedRecordsAsync(
            IMongoCollection<BsonDocument> sourceCollection,
            CancellationToken stoppingToken);
    }
}
