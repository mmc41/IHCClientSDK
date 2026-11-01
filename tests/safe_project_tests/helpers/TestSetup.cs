using System;
using Microsoft.Extensions.Configuration;
using Ihc;
using Ihc.Tests.Shared;

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

        /// <summary>This suite's own proof that its tests run latched; see <see cref="ControllerLatchProbe"/>.</summary>
        [OneTimeSetUp]
        public void ProveTheRunIsLatched() =>
            ControllerLatchProbe.RequireRefused(new AuthenticationService(new IhcSettings { Endpoint = ControllerLatchProbe.Endpoint }));

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("ihcsettings.json", optional: true)
                .Build();

            if (config.GetSection("ihcclient").Exists())
            {
                // The endpoint and the credentials never come from the developer's file: this suite is
                // controller-free, so nothing it runs may reach a controller or carry a login. Every other setting
                // still does, so a test sees the developer's flags and the reference-catalog install directory.
                // The excluded fields keep a clean checkout's values, so they read the same on every machine.
                IhcSettings clean = new IhcSettings();
                Settings = IhcSettings.GetFromConfiguration(config) with
                {
                    Endpoint = clean.Endpoint,
                    UserName = clean.UserName,
                    Password = clean.Password
                };
            }
        }
    }
}
