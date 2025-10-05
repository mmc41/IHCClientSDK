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
        const string CMD_DOWNLOAD = "DOWNLOAD";
        const string CMD_UPLOAD = "UPLOAD";

        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Expected arguments: 'DOWNLOAD <destfile>' OR 'UPLOAD <sourcefile>'");
                return;
            }

            string command = args[0].ToUpper();
            string path = args[1];
            if (command != CMD_DOWNLOAD && command != CMD_UPLOAD)
            {
                Console.WriteLine("Illegal command. Expected 'DOWNLOAD' or 'UPLOAD'");
                return;
            }

            if (command == CMD_UPLOAD && !File.Exists(path))
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
            var ihcConfig = config.GetSection("ihcConfig");
            var endpoint = ihcConfig["endpoint"];
            var userName = ihcConfig["userName"];
            var password = ihcConfig["password"];
            var application = ihcConfig["application"];

            // Create client for IHC services that this utility use:
            var authService = new AuthenticationService(logger, endpoint);
            var controllerService = new ControllerService(authService);
            try
            {
                // Authenticate against IHC system. 
                var login = await authService.Authenticate(userName, password, application);

                if (command == CMD_DOWNLOAD)
                {
                    ProjectFile project = await controllerService.GetProject();
                    File.WriteAllText(path, project.Data, project.Encoding);
                    Console.WriteLine($"Downloaded project to {path}, size {project.Data.Length} characters (Org filename was {project.Filename})");
                }
                else if (command == CMD_UPLOAD)
                {
                    var projectContent = File.ReadAllText(path);
                    // await controllerService.UploadProject(projectData);
                    Console.WriteLine($"Uploaded project from {path}, size {projectContent.Length} bytes");
                }
            }
            finally
            {
                await authService.Disconnect();
            }
        }
    }
}
