using Ihc.App;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;

namespace Ihc.example
{
    /// <summary>
    /// Read IHC system information such as system version, license info, number of users, modules and resources.
    /// </summary>
    class Program
    {
        public const string AppServiceName = "IhcInfoConsole";
        public const string AppServiceNamespace = "Ihc";
        public const string ActivitySourceName = "IhcLab";
        public static ActivitySource ActivitySource { get; } = new ActivitySource(name: ActivitySourceName);

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

                 using var telmetryTracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetErrorStatusOnException(true)
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: AppServiceName, serviceNamespace: AppServiceNamespace))
                    .AddSource(Ihc.Telemetry.ActivitySourceName, ActivitySourceName)
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(telemetryConfig.Traces);
                        if (!string.IsNullOrEmpty(telemetryConfig.Headers))
                            opts.Headers = telemetryConfig.Headers;
                        opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    }).Build();

                // Create client information app service.
                using (InformationService infoService = new InformationService(settings))
                {
                    var info = await infoService.GetInformationModel();
                    Console.WriteLine($"IHC information: {info}");
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed operation: {ex.Message} : {ex.StackTrace}");
            }
        }
    }
}

