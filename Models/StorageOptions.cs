namespace StorageSyncWorker.Models
{
    public record StorageOptions
    {
        public string SourceDatabaseConnection { get; init; }
        public string TargetDatabaseConnection { get; init; }
        public string SourceDatabase { get; init; }
        public string TargetDatabase { get; init; }
        public string[] CollectionNames { get; init; }
        public int MaxPoolSize { get; init; }
    }
}
