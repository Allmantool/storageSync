using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;
using StorageSyncWorker.Factories;
using StorageSyncWorker.Models;

namespace StorageSyncWorker;

public class MongoSyncWorker : BackgroundService
{
    private readonly ILogger<MongoSyncWorker> _logger;
    private readonly IOperationHandlersFactory _operationHandlersFactory;
    private readonly StorageOptions _storageOptions;

    private readonly IMongoDatabase _sourceDatabase;

    public MongoSyncWorker(
        ILogger<MongoSyncWorker> logger,
        IOperationHandlersFactory operationHandlersFactory,
        IOptions<StorageOptions> storageOptions)
    {
        _storageOptions = storageOptions.Value;

        _logger = logger;
        _operationHandlersFactory = operationHandlersFactory;

        var sourceClient = new MongoClient(_storageOptions.SourceDatabaseConnection);
        _sourceDatabase = sourceClient.GetDatabase(_storageOptions.SourceDatabase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Start watching change streams at: " + DateTime.Now);

        var sourceCollections = _storageOptions.CollectionNames
            .Select(name => _sourceDatabase.GetCollection<BsonDocument>(name))
            .ToList();

        var watchTasks = sourceCollections.Select(col => WatchChangeStreamAsync(col, stoppingToken));

        await Task.WhenAll(watchTasks);
    }

    private async Task WatchChangeStreamAsync(
        IMongoCollection<BsonDocument> sourceCollection,
        CancellationToken stoppingToken)
    {
        var sourceName = sourceCollection.CollectionNamespace.CollectionName;
        using var changeStream = await sourceCollection.WatchAsync(cancellationToken: stoppingToken);
        await changeStream.ForEachAsync(async change =>
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            var handler = _operationHandlersFactory.GetHandler(change.OperationType);

            if (handler == null)
            {
                return;
            }

            await handler.HandleAsync(change, sourceName);
        }, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        await base.StopAsync(stoppingToken);

        _logger.LogInformation("MongoDB Change Stream Service is stopping.");
    }
}