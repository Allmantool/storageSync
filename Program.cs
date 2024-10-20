using StorageSyncWorker.Extensions.Logs;
using StorageSyncWorker.Factories;
using StorageSyncWorker.Models;

namespace StorageSyncWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            var services = builder.Services;
            var environment = builder.Environment;
            var configuration = builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true)
                .Build();

            services.Configure<StorageOptions>(builder.Configuration.GetSection("StorageOptions"));
            services.AddHostedService<MongoSyncWorker>();
            services.AddSingleton<IOperationHandlersFactory, OperationHandlersFactory>();

            configuration.InitializeLogger(environment, builder);

            var host = builder.Build();
            
            host.Run();
        }
    }
}