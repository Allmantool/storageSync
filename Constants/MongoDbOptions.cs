namespace StorageSyncWorker.Constants
{
    internal static class MongoDbOptions
    {
        public const int ConnectTimeoutInSeconds = 30;
        public const int ServerSelectionTimeoutInSeconds = 90;
        public const int DelayBetweenSyncAttemptsInSeconds = 5;
        public const int DelayBetweenDeleteAttemptsInSeconds = 120;
        public const int MaxDataAliveInDays = 620;
        public const int DeleteBunchSize = 50;
        public const int DelayBetweenCleanUpInMinutes = 3;
    }
}
