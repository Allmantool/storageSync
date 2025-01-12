using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

using StorageSyncWorker.Constants;
using StorageSyncWorker.Models;
using StorageSyncWorker.Services;

namespace StorageSyncWorker;

public class MongoSyncWorker : BackgroundService
{
    private readonly ILogger<MongoSyncWorker> _logger;
    private readonly IDataReplicationService _dataReplicationService;
    private readonly ICleanUpService _cleanUpService;
    private readonly StorageOptions _storageOptions;
    private readonly MongoClient _mongoClient;

    public MongoSyncWorker(
        ILogger<MongoSyncWorker> logger,
        IOptions<StorageOptions> storageOptions,
        IDataReplicationService dataReplicationService,
        ICleanUpService cleanUpService)
    {
        _logger = logger;
        _dataReplicationService = dataReplicationService;
        _storageOptions = storageOptions.Value;

        var mongoClientSettings = MongoClientSettings.FromConnectionString(_storageOptions.SourceDatabaseConnection);
        mongoClientSettings.ConnectTimeout = TimeSpan.FromSeconds(MongoDbOptions.ConnectTimeoutInSeconds);
        mongoClientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(MongoDbOptions.ServerSelectionTimeoutInSeconds);
        mongoClientSettings.RetryReads = true;
        mongoClientSettings.RetryWrites = true;
        mongoClientSettings.MaxConnecting = _storageOptions.MaxPoolSize;

        _mongoClient = new MongoClient(mongoClientSettings);
        _cleanUpService = cleanUpService;
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

            // Ensure 'sysTime' index exists for each collection
            foreach (var collection in sourceCollections)
            {
                await EnsureSysTimeIndexAsync(collection);
            }

            var watchAndSyncTasks = sourceCollections.Select(col => _dataReplicationService.WatchAndSyncChangeStreamAsync(col, stoppingToken));

            var cleanupTasks = sourceCollections.Select(col => _cleanUpService.RemoveOutdatedRecordsAsync(col, stoppingToken));

            await Task.WhenAll(watchAndSyncTasks.Concat(cleanupTasks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while executing MongoDB Change Stream Worker.");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping MongoDB Change Stream Worker at: {Time}", DateTime.Now);
        await base.StopAsync(stoppingToken);
    }

    private async Task EnsureSysTimeIndexAsync(IMongoCollection<BsonDocument> collection)
    {
        var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(FieldNames.DateField);

        // Check if the index already exists
        var existingIndexes = await collection.Indexes.ListAsync();
        var indexes = await existingIndexes.ToListAsync();

        if (!indexes.Any(index => index["key"].AsBsonDocument.Contains(FieldNames.DateField)))
        {
            _logger.LogInformation("Creating index on 'sysTime' for collection: {CollectionName}", collection.CollectionNamespace.CollectionName);

            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(indexKeys),
                new CreateOneIndexOptions()
            );

            _logger.LogInformation("Index on 'sysTime' created for collection: {CollectionName}", collection.CollectionNamespace.CollectionName);
        }
        else
        {
            _logger.LogInformation("Index on 'sysTime' already exists for collection: {CollectionName}", collection.CollectionNamespace.CollectionName);
        }
    }
}
