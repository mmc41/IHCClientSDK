using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Projects;

namespace safe_visual_tests;

/// <summary>
/// File→New must produce the same document IHC Visual's File→New produces (uxparity S-01): the authentic
/// locality names that live in the <c>.vis</c> as data, the stored container name shown on the root row, and the
/// installer/programmer identity seeded from application settings + the current OS user.
/// </summary>
public class NewProjectTemplateParityTests
{
    private static InstallerIdentityStore Store(string dir, string user = "Morten Christensen") =>
        new(Path.Combine(dir, "installer.json"), user);

    [Test]
    public async Task New_SeedsTheVendorLocalityNames_BecauseRoomNamesAreProjectData()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();

        string?[] names = harness.Session.Current!.Groups
            .Select(g => harness.Session.Current!.View(g).Name).ToArray();

        Assert.That(names, Is.EqualTo(new[]
        {
            "Stue", "Entré", "Køkken", "Soveværelse", "Værelse",
            "Bad", "Bryggers", "Garage", "Kælder", "Udendørs",
        }), "the default room names are project file content, not UI text, so they match the file format's template");
    }

    [Test]
    public async Task LocalitiesRoot_ShowsTheContainerNameStoredInTheProject()
    {
        using var harness = ShellHarness.Create();
        await harness.Session.StartAsync();

        var projector = new ProjectTreeProjector(harness.Session.Current!);

        Assert.That(projector.BuildLocalitiesRoot(functions: false).DisplayName, Is.EqualTo("Lokaliteter"),
            "the root row renders the <groups> element's stored name, not a hard-coded caption");
    }

    [Test]
    public async Task New_SeedsInstallerIdentityFromSettings_AndProgrammerFromTheOsUser()
    {
        using var harness = ShellHarness.Create();
        harness.Session.InstallerIdentity.Update(new InstallerIdentity { Name = "Morten", Country = "Danmark" });

        await harness.Session.StartAsync();
        Project project = harness.Session.Current!;

        Assert.Multiple(() =>
        {
            Assert.That(project.InstallerName, Is.EqualTo("Morten"));
            Assert.That(project.InstallerCountry, Is.EqualTo("Danmark"));
            Assert.That(project.Programmer, Is.EqualTo(harness.Session.InstallerIdentity.Programmer));
        });
    }

    [Test]
    public void InstallerIdentityStore_RoundTripsThroughItsFile_AndOmitsUnsetFields()
    {
        using var harness = ShellHarness.Create();
        var store = Store(harness.TempDir);
        store.Update(new InstallerIdentity { Name = "Morten", Country = "Danmark" });

        var reloaded = Store(harness.TempDir);
        ProjectDetails details = reloaded.NewProjectDetails();

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Identity.Name, Is.EqualTo("Morten"));
            Assert.That(reloaded.Identity.Country, Is.EqualTo("Danmark"));
            Assert.That(details.Programmer, Is.EqualTo("Morten Christensen"), "programmer comes from the OS user");
            Assert.That(details.InstallerAddress, Is.Null, "an unset field is not written to the project");
        });
    }
}
