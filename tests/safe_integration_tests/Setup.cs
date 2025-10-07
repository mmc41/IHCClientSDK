using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Ihc;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Ihc.Tests
{
    /**
    * Setup globals for configuration and logging that is run before any tests and provide tests with needed config data.
    **/
    [SetUpFixture]
    public class Setup
    {
        public static ILogger logger;
        public static string endpoint;
        public static string userName;
        public static string password;
        public static string application;
        public static bool logSensitiveData;
        public static int boolOutput1;
        public static int boolInput1;
        public static int boolInput2;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
          IConfigurationRoot config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))
                    .AddJsonFile("ihcsettings.json")
                    .Build();

          using var loggerFactory = LoggerFactory.Create(builder => {
                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddDebug();
          });

          logger = loggerFactory.CreateLogger<Setup>(); // or use NullLogger<Setup>.Instance;

          var ihcConfig = config.GetSection("ihcConfig");
          endpoint = ihcConfig["endpoint"];
          userName = ihcConfig["userName"];
          password = ihcConfig["password"];
          application = ihcConfig["application"];
          logSensitiveData = bool.Parse(ihcConfig["logSensitiveData"]);

          var testConfig = config.GetSection("testConfig");
          boolOutput1 = int.Parse(testConfig["boolOutput1"]);
          boolInput1 = int.Parse(testConfig["boolInput1"]);
          boolInput2 = int.Parse(testConfig["boolInput2"]);
        }
    }
}