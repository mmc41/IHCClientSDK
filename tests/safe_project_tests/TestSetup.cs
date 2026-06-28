using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Ihc;

// Disable parallel test execution for the assembly (matches the other test suites).
[assembly: NonParallelizable]

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Loads optional IHC settings before any test runs. A clean checkout has no ihcsettings.json, so
    /// the install-dir setting stays empty and install-dir-gated tests skip gracefully (matching the
    /// pattern in safe_integration_tests). Stage 1 has no running tests, but the field is needed so the
    /// illustrative authoring-API preview compiles.
    /// </summary>
    [SetUpFixture]
    public class TestSetup
    {
        public static IhcSettings Settings { get; private set; } = new IhcSettings();

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            string location = Assembly.GetEntryAssembly()?.Location ?? AppContext.BaseDirectory;
            string basePath = Path.GetDirectoryName(location) ?? AppContext.BaseDirectory;

            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("ihcsettings.json", optional: true)
                .Build();

            if (config.GetSection("ihcclient").Exists())
            {
                Settings = IhcSettings.GetFromConfiguration(config);
            }
        }
    }
}
