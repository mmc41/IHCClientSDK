using System;
using Microsoft.Extensions.Configuration;
using Ihc;

// Disable parallel test execution for the assembly (matches the other test suites).
[assembly: NonParallelizable]

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Loads optional settings before any test runs. A clean checkout has no ihcsettings.json, so the reference
    /// catalog option stays empty and disk-backed catalog comparisons skip gracefully.
    /// </summary>
    [SetUpFixture]
    public class TestSetup
    {
        public static IhcSettings Settings { get; private set; } = new IhcSettings();

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("ihcsettings.json", optional: true)
                .Build();

            if (config.GetSection("ihcclient").Exists())
            {
                Settings = IhcSettings.GetFromConfiguration(config);
            }
        }
    }
}
