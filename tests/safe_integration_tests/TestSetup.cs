using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Ihc;
using System.Reflection;

// Disable parallel test execution for entire assembly due to shared hardware state
[assembly: NonParallelizable]

namespace Ihc.Tests
{
  /**
  * Setup globals for configuration that is run before any tests and provide tests with needed config data.
  **/
  [SetUpFixture]
  public class Setup
  {
    public static IhcSettings settings;
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

      settings = IhcSettings.GetFromConfiguration(config);

      var testConfig = config.GetSection("testConfig");
      boolOutput1 = int.Parse(testConfig["boolOutput1"]);
      boolInput1 = int.Parse(testConfig["boolInput1"]);
      boolInput2 = int.Parse(testConfig["boolInput2"]);
    }
  }
}