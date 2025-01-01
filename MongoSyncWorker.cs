using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Constants;
using StorageSyncWorker.Factories;
using StorageSyncWorker.Models;

namespace StorageSyncWorker;

public class MongoSyncWorker : BackgroundService
{
    private readonly ILogger<MongoSyncWorker> _logger;
    private readonly IOperationHandlersFactory _operationHandlersFactory;
    private readonly StorageOptions _storageOptions;
    private readonly MongoClient _mongoClient;

    public MongoSyncWorker(
        ILogger<MongoSyncWorker> logger,
        IOperationHandlersFactory operationHandlersFactory,
        IOptions<StorageOptions> storageOptions)
    {
        _logger = logger;
        _operationHandlersFactory = operationHandlersFactory;
        _storageOptions = storageOptions.Value;

        var mongoClientSettings = MongoClientSettings.FromConnectionString(_storageOptions.SourceDatabaseConnection);
        mongoClientSettings.ConnectTimeout = TimeSpan.FromSeconds(MongoDbOptions.ConnectTimeoutInSeconds);
        mongoClientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(MongoDbOptions.ServerSelectionTimeoutInSeconds);
        mongoClientSettings.RetryReads = true;
        mongoClientSettings.RetryWrites = true;

        _mongoClient = new MongoClient(mongoClientSettings);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting MongoDB Change Stream Worker at: {Time}", DateTime.Now);

        try
        {
            var sourceDatabase = _mongoClient.GetDatabase(_storageOptions.SourceDatabase);

            var sourceCollections = _storageOptions.CollectionNames
                .Select(name => sourceDatabase.GetCollection<BsonDocument>(name))
                .ToList();

            var watchTasks = sourceCollections.Select(col => WatchChangeStreamAsync(col, stoppingToken));

            await Task.WhenAll(watchTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while executing MongoDB Change Stream Worker.");
        }
    }

    private async Task WatchChangeStreamAsync(
        IMongoCollection<BsonDocument> sourceCollection,
        CancellationToken stoppingToken)
    {
        var sourceName = sourceCollection.CollectionNamespace.CollectionName;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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
                        _logger.LogWarning(
                            "No handler found for operation type {OperationType} in collection {CollectionName}",
                            change.OperationType,
                            sourceName);

                        return;
                    }

                    try
                    {
                        await handler.HandleAsync(change, sourceName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling change for collection {CollectionName}", sourceName);
                    }
                }, stoppingToken);
            }
            catch (MongoException ex) when (ex is MongoConnectionException or MongoExecutionTimeoutException)
            {
                _logger.LogError(ex, "MongoDB connection issue detected for collection {CollectionName}. Retrying in 5 seconds...", sourceName);
                await Task.Delay(TimeSpan.FromSeconds(MongoDbOptions.DelayBetweenAttemptsInSeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while watching collection {CollectionName}. Retrying in 5 seconds...", sourceName);
                await Task.Delay(TimeSpan.FromSeconds(MongoDbOptions.DelayBetweenAttemptsInSeconds), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping MongoDB Change Stream Worker at: {Time}", DateTime.Now);
        await base.StopAsync(stoppingToken);
    }
}
