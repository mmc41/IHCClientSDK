using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Ihc.example
{
    /// <summary>
    /// Basic example of how to manipulate inputs and output resources. Requires use of test name/password and test resource IDs specified in configuration file.
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

            // Use this way to read IHC client settings from configuration file as it decrypts sensitive data if encryption is enabled.
            IhcSettings settings = IhcSettings.GetFromConfiguration(config);

            // Read additional configuration settings
            var testConfig = config.GetSection("testConfig");
            var boolOutput1 = int.Parse(testConfig["boolOutput1"]);
            var boolInput1 = int.Parse(testConfig["boolInput1"]);
            var boolInput2 = int.Parse(testConfig["boolInput2"]);

            // Create client for IHC services that this example use (see also ConfigurationService, MessageControlLogService, ModuleService, NotificationManagerService, OpenAPIService, TimeManagerService, UserManagerService).
            var authService = new AuthenticationService(settings);
            var resourceInteractionService = new ResourceInteractionService(authService);
            try
            {
                // Authenticate against IHC system. 
                var login = await authService.Authenticate(); // Use username/password from settings

                // Get value of a bool input
                var inputValue = await resourceInteractionService.GetRuntimeValue(boolInput1);
                string inputStat = inputValue.Value.BoolValue.Value ? "ON" : "OFF";

                Console.WriteLine($"Resource with ID {boolInput1} is {inputStat}");

                // Toggle a bool output
                var outputValue = await resourceInteractionService.GetRuntimeValue(boolOutput1);
                string outputStat = outputValue.Value.BoolValue.Value ? "ON" : "OFF";
                Console.WriteLine($"Resource with ID {boolOutput1} was {outputStat}");
                var reverseValue = ResourceValue.ToogleBool(outputValue);
                var toggledOutput = await resourceInteractionService.SetResourceValue(reverseValue);
                outputStat = toggledOutput ? "ON" : "OFF";
                Console.WriteLine($"Resource with ID {boolOutput1} is now {outputStat}");
            }
            finally
            {
                await authService.Disconnect();
            }
        }
    }
}
