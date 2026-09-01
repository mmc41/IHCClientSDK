using System;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Configuration;

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
            IConfigurationRoot config = IhcConfiguration.FromAppDirectory();

            // Use this way to read IHC client settings from configuration file as it decrypts sensitive data if encryption is enabled.
            IhcSettings settings = IhcSettings.GetFromConfiguration(config);

            // Read additional configuration settings
            var testConfig = config.GetSection("testConfig");
            var boolOutput1 = int.Parse(RequiredSetting(testConfig, "boolOutput1"));
            var boolInput1 = int.Parse(RequiredSetting(testConfig, "boolInput1"));
            var boolInput2 = int.Parse(RequiredSetting(testConfig, "boolInput2"));

            // Create client for IHC services that this example use (see also ConfigurationService, MessageControlLogService, ModuleService, NotificationManagerService, OpenAPIService, TimeManagerService, UserManagerService).
            using var authService = new AuthenticationService(settings);
            var resourceInteractionService = new ResourceInteractionService(authService);
            try
            {
                // Authenticate against IHC system. 
                var login = await authService.Authenticate(); // Use username/password from settings

                // Get value of a bool input
                var inputValue = await resourceInteractionService.GetRuntimeValue(boolInput1);
                string inputStat = BoolOf(inputValue, boolInput1) ? "ON" : "OFF";

                Console.WriteLine($"Resource with ID {boolInput1} is {inputStat}");

                // Toggle a bool output
                var outputValue = await resourceInteractionService.GetRuntimeValue(boolOutput1);
                string outputStat = BoolOf(outputValue, boolOutput1) ? "ON" : "OFF";
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

        /// <summary>
        /// Reads a setting the example cannot run without, so a missing entry is reported by name.
        /// </summary>
        static string RequiredSetting(IConfigurationSection section, string key)
            => section[key] ?? throw new InvalidOperationException($"Configuration setting '{section.Path}:{key}' is required to run this example.");

        /// <summary>
        /// Reads the boolean out of a resource value. Only the member matching ValueKind is set,
        /// so asking a non-boolean resource for one is a configuration mistake worth naming.
        /// </summary>
        static bool BoolOf(ResourceValue value, int resourceId)
            => value.Value.BoolValue ?? throw new InvalidOperationException($"Resource with ID {resourceId} returned a {value.Value.ValueKind} value, not a boolean.");
    }
}
