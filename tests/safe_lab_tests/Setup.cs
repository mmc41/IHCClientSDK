using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Ihc;
using System.Reflection;
using IhcLab;

namespace Ihc.Tests
{
    /**
    * Setup globals for configuration that is run before any tests and provide tests with needed config data.
    **/
    [SetUpFixture]
    public class Setup
    {
        public static IhcSettings? settings;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
          var assemblyLocation = Assembly.GetEntryAssembly()?.Location;
          if (assemblyLocation == null)
          {
              throw new InvalidOperationException("Unable to determine assembly location");
          }

          var basePath = Path.GetDirectoryName(assemblyLocation);
          if (basePath == null)
          {
              throw new InvalidOperationException("Unable to determine base path from assembly location");
          }

          IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("ihcsettings.json")
                .Build();

          settings = config.GetSection("ihcclient").Get<IhcSettings>();
            if (settings == null)
            {
                throw new InvalidOperationException("Unable to load IHC settings from configuration");
            }
          
            IhcLab.Program.config = new Configuration();
        }
    }
}