using System;
using System.IO;
using Avalonia.Headless;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(Ihc.Tests.Shared.OpenVisualHeadlessApp))]

// Sequential for the reasons safe_visual_tests gives: telemetry captures listen by scope, the task supervisor's
// fault port is one static, and the leak detector's count is shared by every test running beside it.
[assembly: NonParallelizable]

namespace safe_visual_stubbed_tests;

using Ihc;
using Ihc.Tests.Shared;

/// <summary>This suite's own proof that its tests run latched; see <see cref="ControllerLatchProbe"/>.</summary>
[SetUpFixture]
public class RunLatchProbe
{
    [OneTimeSetUp]
    public void ProveTheRunIsLatched() =>
        ControllerLatchProbe.RequireRefused(new AuthenticationService(new IhcSettings { Endpoint = ControllerLatchProbe.Endpoint }));
}

/// <summary>
/// Fails the run, never ignores it, when a controller settings file sits beside the assembly. This suite stages
/// none, so one being there means something put a real controller's settings where a test could read them.
/// </summary>
[SetUpFixture]
public class NoSettingsFileBesideTheSuite
{
    internal const string SettingsFileName = "ihcsettings.json";

    [OneTimeSetUp]
    public void RefuseASettingsFile()
    {
        if (SettingsFileRefusal(AppContext.BaseDirectory) is { } refusal)
        {
            Assert.Fail(refusal);
        }
    }

    /// <summary>Why a run from <paramref name="directory"/> must not go on, or null when it may.</summary>
    internal static string? SettingsFileRefusal(string directory) =>
        File.Exists(Path.Combine(directory, SettingsFileName))
            ? $"a {SettingsFileName} sits beside the suite in {directory}; this suite runs only against stubs and must "
              + "not be able to read a controller's settings — remove the file"
            : null;
}
