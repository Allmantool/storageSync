namespace StorageSyncWorker.Models
{
    public record StorageOptions
    {
        public string SourceDatabaseConnection { get; set; }
        public string TargetDatabaseConnection { get; set; }
        public string SourceDatabase { get; set; }
        public string TargetDatabase { get; set; }
        public string[] CollectionNames { get; set; }
    }
}
