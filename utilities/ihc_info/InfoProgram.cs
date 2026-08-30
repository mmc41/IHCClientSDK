using Ihc.App;
using Microsoft.Extensions.Configuration;
using Ihc.Bootstrap;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ihc.example
{
    /// <summary>
    /// Read IHC system information such as system version, license info, number of users, modules and resources.
    /// </summary>
    class Program
    {
        public const string AppServiceName = "IhcInfoConsole";
        public const string AppServiceNamespace = "Ihc";
        // Equal to AppServiceName, as in every other host. They were "IhcInfoConsole" and "IhcInfo",
        // so this utility reported one identity as its service and another as its scope.
        public const string ActivitySourceName = AppServiceName;
        public static ActivitySource ActivitySource { get; } =
            new ActivitySource(name: ActivitySourceName, version: TelemetryBootstrap.GetAppVersionStr());

        static async Task Main(string[] args)
        {
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();

            // Read configuration settings
            var settings = IhcSettings.GetFromConfiguration(config);
            var telemetryConfig = TelemetryConfiguration.GetFromConfiguration(config);

            try
            {
                // Both providers, each gated on its configured endpoint, from the one builder every IHC host
                // shares (R7). The hand-rolled copy this replaces tested the endpoint INSIDE the exporter
                // callback, so an unconfigured utility still built a provider and still exported - to the OTLP
                // default endpoint. It also gains this utility metrics, which it never had.
                using ILoggerFactory loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                    AppServiceName, AppServiceNamespace, ActivitySourceName, telemetryConfig, config);

                // Create client information app service.
                using (InformationAppService infoService = new InformationAppService(settings))
                {
                    var info = await infoService.GetInformationModel();

                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Converters = { new JsonStringEnumConverter() }
                    };

                    string json = JsonSerializer.Serialize(info, jsonOptions);

                    Console.WriteLine($"IHC information: {json}");
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed operation: {ex.Message} : {ex.StackTrace}");
            }

            // A console process exits as soon as this returns, so the flush happens here rather than in a
            // finalizer that will not run.
            TelemetryBootstrap.Shutdown();
        }
    }
}

