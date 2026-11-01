using System.IO;
using System.Linq;
using System.Reflection;
using ihc_openvisual.Configuration;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The copying constructor start-up rebuilds the configuration through, once the controller-stub switch has chosen
/// the controller: only the controller settings change, and everything else a reader shows stays as loaded.
/// </summary>
[TestFixture]
public class AppConfigurationCopyTests
{
    private static readonly IhcSettings Replacement = new()
    {
        Endpoint = "http://s1.controllerstub.invalid",
        UserName = "stub-user",
        Password = "stub-pw",
        AsyncContinueOnCapturedContext = true,
    };

    /// <summary>
    /// Every public property but the controller settings, read by reflection, so a property added to the class later
    /// and not copied fails here rather than reverting silently to a default on the stub path.
    /// </summary>
    [Test]
    public void EveryOtherPublicProperty_IsTheSourcesOwn()
    {
        using ScratchDir dir = new("ihc_ov_copy_");
        File.WriteAllText(dir.File("ihcsettings.json"),
            """{ "Logging": { "LogLevel": { "Default": "Debug" } }, "telemetry": { "Logs": "http://collector.invalid/v1/logs" } }""");
        var source = new AppConfiguration(dir.Path);

        var copy = new AppConfiguration(source, Replacement);

        PropertyInfo[] others = [.. typeof(AppConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != nameof(AppConfiguration.IhcSettings))];
        Assert.That(others, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (PropertyInfo property in others)
            {
                Assert.That(property.GetValue(copy), Is.SameAs(property.GetValue(source)).Or.EqualTo(property.GetValue(source)),
                    property.Name);
            }
        });
        Assert.That(copy.SettingsFileFound, Is.True, "the source was read from a real file, so the copy reports it");
    }

    [Test]
    public void TheControllerSettings_AreTheReplacement_SettledOffTheUiContext()
    {
        using ScratchDir empty = new("ihc_ov_copy_");
        var copy = new AppConfiguration(new AppConfiguration(empty.Path), Replacement);

        Assert.That(copy.IhcSettings, Is.EqualTo(Replacement with { AsyncContinueOnCapturedContext = false }));
    }
}
