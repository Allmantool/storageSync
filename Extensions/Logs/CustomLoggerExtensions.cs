using System.Threading.Channels;

using Elastic.Apm.SerilogEnricher;
using Elastic.Channels;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Serilog;
using Serilog.Core;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Exceptions;

namespace StorageSyncWorker.Extensions.Logs
{
    internal static class CustomLoggerExtensions
    {
        public static Logger InitializeLogger(
            this IConfiguration configuration,
            IHostEnvironment environment,
            ILoggingBuilder loggingBuilder)
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithExceptionDetails()
                .Enrich.WithProperty("Environment", environment)
                .Enrich.WithProperty("Host-service", "MongoSyncWorker")
                .Enrich.WithSpan()
                .WriteTo.Debug()
                .WriteTo.Console()
                .AddElasticSearchSupport(configuration, environment)
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(logger);

            return logger;
        }

        private static LoggerConfiguration AddElasticSearchSupport(
            this LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var elasticNodeUrl = (configuration["ElasticConfiguration:Uri"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) ?? string.Empty;

            return string.IsNullOrWhiteSpace(elasticNodeUrl)
                ? loggerConfiguration
                : loggerConfiguration
                    .Enrich.WithElasticApmCorrelationInfo()
                    .WriteTo.Elasticsearch(
                        new List<Uri>
                        {
                            new(elasticNodeUrl)
                        },
                        opt => opt.ConfigureElasticSink(environment));
        }

        private static void ConfigureElasticSink(this ElasticsearchSinkOptions options, IHostEnvironment environment)
        {
            var formattedExecuteAssemblyName = typeof(Program).Assembly.GetName().Name;
            var dateIndexPostfix = DateTime.UtcNow.ToString("MM-yyyy-dd");

            options.DataStream = new DataStreamName($"{formattedExecuteAssemblyName}-{environment.EnvironmentName}-{dateIndexPostfix}".Replace(".", "-").ToLower());
            options.BootstrapMethod = BootstrapMethod.Failure;
            options.MinimumLevel = LogEventLevel.Debug;
            options.ConfigureChannel = channelOpts =>
            {
                channelOpts.BufferOptions = new BufferOptions
                {
                    BoundedChannelFullMode = BoundedChannelFullMode.DropNewest,
                };
            };
        }
    }
}