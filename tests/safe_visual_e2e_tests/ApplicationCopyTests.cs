using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Ihc;
using Ihc.Tests;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The copy every desktop launch starts from (O8), checked without a desktop: the settings it is given from a
/// developer's file, the check that refuses a launch from anything else, and the refresh that keeps it the
/// application the last build produced. All run in the headless leg.
/// </summary>
[TestFixture]
public sealed class ApplicationCopyTests
{
    private const string RealEndpoint = "https://192.0.2.10";
    private const string RealUser = "developer-user";
    private const string RealPassword = "developer-password";

    /// <summary>
    /// A developer's file in the shape the application reads: a real controller and its login, the encryption
    /// marker, telemetry carrying a credential of its own under a key cased differently from the copy's, logging,
    /// and a section the copy has no use for.
    /// </summary>
    private const string DeveloperSettings = $$"""
        {
          // The reader the application uses allows comments and trailing commas, so the copy's writer must too.
          "ihcclient": { "endpoint": "{{RealEndpoint}}", "userName": "{{RealUser}}", "password": "{{RealPassword}}" },
          "encryption": { "isEncrypted": false },
          "Telemetry": { "host": "http://localhost:5080", "headers": { "Authorization": "Basic developer-telemetry" } },
          "Logging": { "LogLevel": { "Default": "Information" } },
          "unrelated": { "kept": false },
        }
        """;

    private readonly List<DirectoryInfo> folders = [];

    [TearDown]
    public void RemoveFolders()
    {
        foreach (DirectoryInfo folder in folders)
        {
            folder.Delete(recursive: true);
        }
        folders.Clear();
    }

    [Test]
    public void TheCopysSettings_KeepTheDevelopersTelemetryAndLogging_AndNothingThatReachesAController()
    {
        const string expected = $$"""
            {
              "ihcclient": { "endpoint": "{{ApplicationCopy.Endpoint}}" },
              "telemetry": { "host": "http://localhost:5080", "headers": { "Authorization": "Basic developer-telemetry" } },
              "Logging": { "LogLevel": { "Default": "Information" } }
            }
            """;

        string written = ApplicationCopy.SettingsFor(DeveloperSettings);

        Assert.That(JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(expected)), Is.True, written);
    }

    [Test]
    public void TheCheck_PassesTheSettingsTheCopyIsGiven()
    {
        Assert.That(ApplicationCopy.SettingsRefusal(FolderWith(ApplicationCopy.SettingsFor(DeveloperSettings))), Is.Null);
    }

    /// <summary>
    /// The seeded breaches: the developer's own file, which is what the application would read from its build
    /// output, and the near misses a hand-edited copy could hold. A refusal never quotes the file.
    /// </summary>
    [TestCase(DeveloperSettings, "outside the reserved .invalid domain", TestName = "{m}(the developer's own file)")]
    [TestCase("""{ "ihcclient": { "endpoint": "http://invalid.example.com" } }""", "outside the reserved .invalid domain",
        TestName = "{m}(invalid as a label, not the domain)")]
    [TestCase("""{ "ihcclient": { "endpoint": "http://no-stub-switch.invalid", "userName": "developer-user" } }""",
        "carries a controller login", TestName = "{m}(a user name)")]
    [TestCase("""{ "ihcclient": { "endpoint": "http://no-stub-switch.invalid", "password": "developer-password" } }""",
        "carries a controller login", TestName = "{m}(a password)")]
    [TestCase("""{ "telemetry": { } }""", "names no controller settings the application can read",
        TestName = "{m}(no controller section)")]
    public void TheCheck_RefusesAnyOtherSettings_WithoutQuotingThem(string settings, string reason)
    {
        string? refusal = ApplicationCopy.SettingsRefusal(FolderWith(settings));

        Assert.That(refusal, Does.Contain(reason)
            .And.Not.Contain(RealEndpoint).And.Not.Contain(RealUser).And.Not.Contain(RealPassword));
    }

    [Test]
    public void TheCopy_FollowsTheBuild_AndNeverHoldsTheStagedSettings()
    {
        DirectoryInfo build = Folder();
        DirectoryInfo copy = Folder();
        File.WriteAllText(Path.Combine(build.FullName, "app.dll"), "first build");
        File.WriteAllText(Path.Combine(build.FullName, "dropped.dll"), "gone from the next build");
        File.WriteAllText(Path.Combine(build.FullName, IhcConfiguration.SettingsFileName), DeveloperSettings);
        CleanCopy.Mirror(build, copy);
        File.WriteAllText(Path.Combine(copy.FullName, IhcConfiguration.SettingsFileName), "written by the last run");

        File.WriteAllText(Path.Combine(build.FullName, "app.dll"), "second build, a different length");
        File.Delete(Path.Combine(build.FullName, "dropped.dll"));
        Directory.CreateDirectory(Path.Combine(build.FullName, "runtimes"));
        File.WriteAllText(Path.Combine(build.FullName, "runtimes", "native.dll"), "new in the second build");
        CleanCopy.Mirror(build, copy);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(Path.Combine(copy.FullName, "app.dll")), Is.EqualTo("second build, a different length"));
            Assert.That(Path.Combine(copy.FullName, "runtimes", "native.dll"), Does.Exist);
            Assert.That(Path.Combine(copy.FullName, "dropped.dll"), Does.Not.Exist);
            Assert.That(Path.Combine(copy.FullName, IhcConfiguration.SettingsFileName), Does.Not.Exist,
                "neither the build's settings nor the last run's");
        });
    }

    private DirectoryInfo Folder()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("ihc_e2e_copy_");
        folders.Add(folder);
        return folder;
    }

    private string FolderWith(string settings)
    {
        DirectoryInfo folder = Folder();
        File.WriteAllText(Path.Combine(folder.FullName, IhcConfiguration.SettingsFileName), settings);
        return folder.FullName;
    }
}
