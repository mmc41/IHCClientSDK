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
            // Read configuration settings
            var settings = IhcSettings.GetFromFile();

            // Create client information app service.
            using (InformationService infoService = new InformationService(settings))
            {
                var info = await infoService.GetInformationModel();
                Console.WriteLine($"IHC information: {info}");
            }
        }
    }
}

