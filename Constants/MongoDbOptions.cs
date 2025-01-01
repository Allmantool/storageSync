namespace StorageSyncWorker.Constants
{
    internal static class MongoDbOptions
    {
        public const int ConnectTimeoutInSeconds = 30;
        public const int ServerSelectionTimeoutInSeconds = 60;
        public const int DelayBetweenAttemptsInSeconds = 5;
    }
}
