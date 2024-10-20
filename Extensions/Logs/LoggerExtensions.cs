﻿namespace StorageSyncWorker.Extensions.Logs
{
    public static partial class LoggerExtensions
    {
        [LoggerMessage(
            EventId = 0,
            Level = LogLevel.Critical,
            Message = "Could not open socket to `{hostName}`")]
        public static partial void CouldNotOpenSocket(
            ILogger logger, string hostName);
    }
}