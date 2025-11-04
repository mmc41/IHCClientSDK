using Ihc.App;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
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
        public const string ActivitySourceName = "IhcInfo";
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
                        if (!string.IsNullOrEmpty(telemetryConfig.Traces))
                        {
                            opts.Endpoint = new Uri(telemetryConfig.Traces);
                            opts.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                            if (!string.IsNullOrEmpty(telemetryConfig.Headers))
                                opts.Headers = telemetryConfig.Headers;
                        }
                    }).Build();

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
        }
    }
}

