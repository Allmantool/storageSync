using MongoDB.Bson;
using MongoDB.Driver;

namespace StorageSyncWorker.Handlers
{
    public interface IOperationHandler
    {
       Task HandleAsync(ChangeStreamDocument<BsonDocument> changeStreamDocument, string sourceName);
    }
}
