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
}
