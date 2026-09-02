using System;
using System.Globalization;
using System.Threading.Tasks;
using Ihc;
using Microsoft.Extensions.Configuration;

namespace Ihc.example
{
    /// <summary>
    /// Basic example of how to listen for changes in inputs. Requires use of test name/password and test resource IDs specified in configuration file.
    /// </summary>
    sealed class Program
    {
        static async Task Main(string[] args)
        {
          // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
          IConfigurationRoot config = IhcConfiguration.FromAppDirectory();

          // Use this way to read IHC client settings from configuration file as it decrypts sensitive data if encryption is enabled.
          IhcSettings settings = IhcSettings.GetFromConfiguration(config);                    

          // Read configuration settings
          var testConfig = config.GetSection("testConfig");
          // Invariant: these are resource ids read from ihcsettings.json, so they are machine-readable
          // configuration text rather than anything the operator's locale should reinterpret.
          var boolOutput1 = int.Parse(RequiredSetting(testConfig, "boolOutput1"), CultureInfo.InvariantCulture);
          var boolInput1 = int.Parse(RequiredSetting(testConfig, "boolInput1"), CultureInfo.InvariantCulture);
          var boolInput2 = int.Parse(RequiredSetting(testConfig, "boolInput2"), CultureInfo.InvariantCulture);

          // Create client for IHC services that this example use (see also ConfigurationService, MessageControlLogService, ModuleService, NotificationManagerService, OpenAPIService, TimeManagerService, UserManagerService).
          using var authService = new AuthenticationService(settings);
          var resourceInteractionService = new ResourceInteractionService(authService);

          // Authenticate against IHC system. 
          var login = await authService.Authenticate(); // Use username/password from settings

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

        /// <summary>
        /// Reads a setting the example cannot run without, so a missing entry is reported by name.
        /// </summary>
        static string RequiredSetting(IConfigurationSection section, string key)
            => section[key] ?? throw new InvalidOperationException($"Configuration setting '{section.Path}:{key}' is required to run this example.");
    }
}
