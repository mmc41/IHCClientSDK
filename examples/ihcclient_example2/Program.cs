using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Ihc.example
{
    /// <summary>
    /// Basic example of how to listen for changes in inputs. Requires use of test name/password and test resource IDs specified in configuration file.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
          // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
          IConfigurationRoot config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))
                    .AddJsonFile("ihcsettings.json")
                    .Build();

          // Create a logger for our application. Alternatively use NullLogger<Setup>.Instance.
          using var loggerFactory = LoggerFactory.Create(builder => {
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

          var testConfig = config.GetSection("testConfig");
          var boolOutput1 = int.Parse(testConfig["boolOutput1"]);
          var boolInput1 = int.Parse(testConfig["boolInput1"]);
          var boolInput2 = int.Parse(testConfig["boolInput2"]);

          // Create client for IHC services that this example use (see also ConfigurationService, MessageControlLogService, ModuleService, NotificationManagerService, OpenAPIService, TimeManagerService, UserManagerService).
          var authService = new AuthenticationService(logger, endpoint);
          var resourceInteractionService = new ResourceInteractionService(authService);

          // Authenticate against IHC system. 
          var login = await authService.Authenticate(userName, password, application);

          // Poll on IO changes to all our input addresses:
          var resourceChanges = resourceInteractionService.GetResourceValueChanges(new int[] {
                                        boolInput1,
                                        boolInput2,
                                });

           await foreach (ResourceValue r in resourceChanges) { // forever loop until CTRL-C.
            Console.WriteLine(r);
           }

           // Clean logout. Not actually executed in this example 
           // but shown for completeness. A real console app should 
           // install a CTRL-C handler to make sure Disconnect is called.
           await authService.Disconnect();
        }
    }
}
