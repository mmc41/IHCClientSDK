using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
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
    public static IhcSettings settings { get; private set; }
    public static int boolOutput1 { get; private set; }
    public static int boolInput1 { get; private set; }
    public static int boolInput2 { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
      Assembly entryAssembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("No entry assembly, so the directory holding ihcsettings.json cannot be resolved.");
      string basePath = Path.GetDirectoryName(entryAssembly.Location)
            ?? throw new InvalidOperationException($"Entry assembly location '{entryAssembly.Location}' has no directory to read ihcsettings.json from.");

      IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("ihcsettings.json", optional: true)
            .Build();

      // A clean checkout has no ihcsettings.json. Skip the controller-dependent integration suite
      // instead of failing OneTimeSetUp when no configuration is present.
      if (!config.GetSection("ihcclient").Exists())
      {
        Assert.Ignore("Integration tests skipped: no ihcsettings.json configuration found.");
      }

      settings = IhcSettings.GetFromConfiguration(config);

      var testConfig = config.GetSection("testConfig");
      boolOutput1 = int.Parse(RequiredSetting(testConfig, "boolOutput1"), CultureInfo.InvariantCulture);
      boolInput1 = int.Parse(RequiredSetting(testConfig, "boolInput1"), CultureInfo.InvariantCulture);
      boolInput2 = int.Parse(RequiredSetting(testConfig, "boolInput2"), CultureInfo.InvariantCulture);

      // Skip all integration tests if endpoint is not configured for real IHC controller
      if (string.IsNullOrEmpty(settings.Endpoint) || settings.Endpoint.StartsWith("mock://", StringComparison.Ordinal))
      {
        Assert.Ignore("Integration tests skipped: Endpoint is null, empty, or starts with 'mock://'");
      }
    }

    /// <summary>
    /// Reads a testConfig entry the suite cannot address a controller without. A configuration
    /// that reached this point has an ihcclient section, so a missing test resource id is a
    /// mistake worth naming rather than an absent configuration to skip over.
    /// </summary>
    private static string RequiredSetting(IConfigurationSection section, string key)
        => section[key] ?? throw new InvalidOperationException($"Configuration setting '{section.Path}:{key}' is required to run the integration tests.");
  }

  /// <summary>
  /// Owns the authenticated controller session that the system-test fixtures share: one
  /// AuthenticationService per test, connected before the test body and disconnected after it.
  /// A derived fixture builds the services it exercises in <see cref="CreateServices"/>.
  /// </summary>
  public abstract class AuthenticatedSystemTest
  {
    private AuthenticationService? authService;

    [SetUp]
    public async Task ConnectAuthenticatedSession()
    {
      authService = new AuthenticationService(Setup.settings);
      CreateServices(authService);
      await authService.Authenticate();
    }

    [TearDown]
    public async Task DisconnectAuthenticatedSession()
    {
      // Null when SetUp failed before assigning, which is why the session is not simply
      // disconnected unconditionally: doing so would mask the failure that came first.
      if (authService is null) return;
      await authService.Disconnect();
      authService.Dispose();
      authService = null;
    }

    /// <summary>
    /// Builds the services the fixture exercises. Called with the session before it is
    /// authenticated, matching the order the fixtures used when each owned this setup.
    /// </summary>
    protected abstract void CreateServices(AuthenticationService session);
  }
}
