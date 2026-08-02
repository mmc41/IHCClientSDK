using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Projects;

namespace safe_visual_tests;

/// <summary>
/// A freshly inserted locality must carry the same placeholder name IHC Visual gives it (uxparity S-07).
/// The name is written into the `.vis` as <c>&lt;group name="…"&gt;</c>, so it is project data like the
/// default room names — a project authored here has to be interchangeable with one authored there.
/// </summary>
public class InsertLocalityParityTests
{
    [Test]
    public async Task InsertLocality_CarriesTheTemplatePlaceholderName_AppendedLast()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.Current!.Groups.Count;

        await vm.InsertLocalityCommand.ExecuteAsync(null);

        Project project = harness.Session.Current!;
        Assert.Multiple(() =>
        {
            Assert.That(project.Groups.Count, Is.EqualTo(before + 1));
            Assert.That(project.View(project.Groups.Last()).Name, Is.EqualTo("Lokalitet"),
                "the placeholder name is project data, so it matches the file format's own");
        });
    }
}
