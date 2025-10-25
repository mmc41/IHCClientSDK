using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text;

namespace Ihc.download_upload_example
{
    /// <summary>
    /// Download or upload an IHC administration file.
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
                Console.WriteLine("Could not find source administration file  " + path);
                return;
            }

            // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
            string basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppContext.BaseDirectory;
            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();

            // Read configuration settings
            var settings = config.GetSection("ihcclient").Get<IhcSettings>();
            if (settings == null)
            {
                Console.WriteLine("Could not read IHC client settings from configuration");
                return;
            }

            // Create client for IHC services that this utility use:
            
            try
            {
                using (var adminServer = new AdminService(settings))
                {
                    if (command == CMD_GET)
                    {
                        AdminModel adminModel = await adminServer.GetModel();
                        string adminJson = System.Text.Json.JsonSerializer.Serialize(adminModel, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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
                Console.WriteLine($"Failed operation: {ex.Message}");
            }
        }
    }
}

