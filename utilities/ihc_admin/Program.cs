using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace Ihc.download_upload_example
{
    /// <summary>
    /// Download or upload an IHC administration file.
    /// </summary>
    class Program
    {
        public const string AppServiceName = "IhcAdminConsole";
        public const string AppServiceNamespace = "Ihc";
        public const string ActivitySourceName = "IhcAdminConsole";
        public static ActivitySource ActivitySource { get; } = new ActivitySource(name: ActivitySourceName);

        const string CMD_GET = "GET";
        const string CMD_STORE = "STORE";

        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine($"Expected arguments: '{CMD_GET} <destfile>' OR '{CMD_STORE} <sourcefile>'");
                return;
            }

            string command = args[0].ToUpper();
            string path = args[1];
            if (command != CMD_GET && command != CMD_STORE)
            {
                Console.WriteLine($"Illegal command. Expected {CMD_GET} or {CMD_STORE}");
                return;
            }

            if (command == CMD_STORE && !File.Exists(path))
            {
                Console.WriteLine("Could not find source administration file  " + path);
                return;
            }

            // Read configuration settings
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();

            var encrypted = EncryptionConfiguration.GetFromConfiguration(config);
            var settings = IhcSettings.GetFromConfiguration(config);
            var telemetryConfig = TelemetryConfiguration.GetFromConfiguration(config);            

            // Create client for IHC services that this utility use:

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
                
                using (var adminServer = new AdminService(settings, encrypted.IsEncrypted))
                {
                    if (command == CMD_GET)
                    {
                        MutableAdminModel adminModel = await adminServer.GetModel();
                        var jsonOptions = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Converters = { new JsonStringEnumConverter() }
                        };
                        string adminJson = JsonSerializer.Serialize(adminModel, jsonOptions);
                        await File.WriteAllTextAsync(path, adminJson, Encoding.UTF8);
                        Console.WriteLine($"Administration file downloaded to {path}");
                    }
                    else if (command == CMD_STORE)
                    {
                        // TODO: Implement upload functionality
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed operation:{ex.Message} : {ex.StackTrace}");
            }
        }
    }
}

