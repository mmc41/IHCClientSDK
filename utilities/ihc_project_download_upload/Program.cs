using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Configuration;
using Ihc.Bootstrap;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;

namespace Ihc.download_upload_example
{      
    /// <summary>
    /// Download or upload an IHC project file
    /// </summary>
    class Program
    {
        public const string AppServiceName = "IhcProjectDownLoadUpLoad";
        public const string AppServiceNamespace = "Ihc";
        public const string ActivitySourceName = "IhcProjectDownLoadUpLoad";
    
        const string CMD_GET = "GET";
        const string CMD_STORE = "STORE";

        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine($"Expected arguments: '{CMD_GET} <destfile>' OR '{CMD_STORE} <sourcefile>'");
                return;
            }

            // Invariant: the command words are ASCII, and a Turkish locale would fold 'i' to 'İ', leaving a
            // correctly typed command unrecognized on that machine alone.
            string command = args[0].ToUpperInvariant();
            string path = args[1];
            if (command != CMD_GET && command != CMD_STORE)
            {
                Console.WriteLine($"Illegal command. Expected {CMD_GET} or {CMD_STORE}");
                return;
            }

            if (command == CMD_STORE && !File.Exists(path))
            {
                Console.WriteLine("Could not find source project file  " + path);
                return;
            }

            // Read configuration settings
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();

            // Read configuration settings
            var settings = IhcSettings.GetFromConfiguration(config);
            var telemetryConfig = TelemetryConfiguration.GetFromConfiguration(config);

            // Create client for IHC services that this utility use:
            using var authService = new AuthenticationService(settings);
            var controllerService = new ControllerService(authService);
            var ressourceService = new ResourceInteractionService(authService);
            var configService = new ConfigurationService(authService);

            try
            {
                // Both providers, each gated on its configured endpoint, from the one builder every IHC host
                // shares (R7). The hand-rolled copy this replaces tested the endpoint INSIDE the exporter
                // callback, so an unconfigured utility still built a provider and still exported - to the OTLP
                // default endpoint. It also gains this utility metrics, which it never had.
                using ILoggerFactory loggerFactory = TelemetryBootstrap.SetupTelemetryAndLogging(
                    AppServiceName, AppServiceNamespace, ActivitySourceName, telemetryConfig, config);

                // Authenticate against IHC system. 
                var login = await authService.Authenticate();

                if (command == CMD_GET)
                {
                    ProjectFile project = await controllerService.GetProject();
                    await File.WriteAllTextAsync(path, project.Data, ProjectFile.Encoding);
                    Console.WriteLine($"Downloaded project to {path}, size {project.Data.Length} characters (Org filename was {project.Filename})");
                }
                else if (command == CMD_STORE)
                {
                    var encoding = ProjectFile.Encoding;
                    ProjectFile project = new ProjectFile(
                        Filename: Path.GetFileName(path),
                        Data: await File.ReadAllTextAsync(path, encoding)
                    );

                    var projectContent = await File.ReadAllTextAsync(path);

                    // TODO: Read all runtime values and store them 

                    bool success = await controllerService.StoreProject(project);
                    if (!success)
                    {
                        Console.WriteLine("Failed to store project to controller");
                        return;
                    }

                    // TODO: Reapply runtime values

                    // Reboot controller to activate new project
                    await configService.DelayedReboot(100);

                    Console.WriteLine($"Sucessfully uploaded project from {path}, size {projectContent.Length} bytes. Rebooting controller.");
                }
            }
            catch (Exception ex)
            {
                // The SDK's own coded problem when it raised one — identity, its Danish sentence and its declared
                // arguments — instead of a bare English message that names no operation (R17: a non-GUI consumer
                // renders the contract too).
                Console.WriteLine($"Failed operation: {ProblemConsoleFormat.Describe(ex)}");
            }
            finally
            {
                await authService.Disconnect();
            }

            // A console process exits as soon as this returns, so the flush happens here rather than in a
            // finalizer that will not run.
            TelemetryBootstrap.Shutdown();
        }
    }
}
