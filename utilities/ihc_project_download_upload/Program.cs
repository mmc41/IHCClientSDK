using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text;

namespace Ihc.download_upload_example
{
    /// <summary>
    /// Download or upload an IHC project file
    /// </summary>
    class Program
    {
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
                Console.WriteLine("Could not find source project file  " + path);
                return;
            }

            // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();

            // Create a logger for our application. Alternatively use NullLogger<Setup>.Instance.
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddConsole();
            });

            var logger = loggerFactory.CreateLogger<Program>();

            // Read configuration settings
            var settings = config.GetSection("ihcclient").Get<IhcSettings>();
            if (settings == null)
            {
                Console.WriteLine("Could not read IHC client settings from configuration");
                return;
            }

            // Create client for IHC services that this utility use:
            var authService = new AuthenticationService(logger, settings);
            var controllerService = new ControllerService(authService);
            var ressourceService = new ResourceInteractionService(authService);
            var configService = new ConfigurationService(authService);

            try
            {
                // Authenticate against IHC system. 
                var login = await authService.Authenticate();

                if (command == CMD_GET)
                {
                    ProjectFile project = await controllerService.GetProject();
                    File.WriteAllText(path, project.Data, ProjectFile.Encoding);
                    Console.WriteLine($"Downloaded project to {path}, size {project.Data.Length} characters (Org filename was {project.Filename})");
                }
                else if (command == CMD_STORE)
                {
                    var encoding = ProjectFile.Encoding;
                    ProjectFile project = new ProjectFile(
                        Filename: Path.GetFileName(path),
                        Data: File.ReadAllText(path, encoding)
                    );

                    var projectContent = File.ReadAllText(path);

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
                Console.WriteLine($"Failed operation: {ex.Message}");
            }
            finally
            {
                await authService.Disconnect();
            }
        }
    }
}
