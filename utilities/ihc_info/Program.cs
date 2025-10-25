using System;
using System.IO;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Ihc.example
{
    /// <summary>
    /// Read IHC system information such as system version, license info, number of users, modules and resources.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Access configuration file that stores IHC and SDK setup informnation including username, password etc.
            string? basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            if (basePath == null)
                throw new InvalidOperationException("Could not determine application base path.");

            IConfigurationRoot config = new ConfigurationBuilder()
                      .SetBasePath(basePath)
                      .AddJsonFile("ihcsettings.json")
                      .Build();

            IhcSettings? settings = config.GetSection("ihcclient").Get<IhcSettings>();

            // Create client information app service.
            using (InformationService infoService = new InformationService(settings))
            {
                var info = await infoService.GetInformationModel();
                Console.WriteLine($"IHC information: {info}");
            }
        }
    }
}

