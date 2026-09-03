using System;
using Microsoft.Extensions.Configuration;
using Ihc;

// Serial by necessity rather than by convention. Tests here read back process-global state that a
// concurrent fixture would be sharing: the telemetry capture, which listens by instrumentation scope
// rather than by owner; the .vis id allocator, whose interleaved allocations change the very bytes a
// byte-fidelity oracle compares; and the edit-analysis full-analysis counter, which a reuse assertion
// reads directly. Measured, not assumed - under ParallelScope.Fixtures this suite failed on both of two
// consecutive runs and the two failure sets differed, so the cost is nondeterminism rather than a
// fixed set of fixtures that could be repaired.
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
