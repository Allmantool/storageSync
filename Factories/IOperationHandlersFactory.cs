using MongoDB.Driver;

using StorageSyncWorker.Handlers;

namespace StorageSyncWorker.Factories
{
    public interface IOperationHandlersFactory
    {
        IOperationHandler GetHandler(ChangeStreamOperationType streamOperationType);
    }
}
