using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;

namespace Ihc.Vis.Tests;

/// <summary>
/// The tree state a project opens in must match what IHC Visual shows (uxparity S-02): only the locality
/// root is open — every locality starts closed, whatever it holds — so the two apps present the same
/// initial overview of the same file.
/// </summary>
public class OpenProjectTreeParityTests
{
    private static string SampleProject() =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis");

    [Test]
    public async Task OpenProject_LocalitiesStartCollapsed_EvenWhenTheyHoldComponents()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(SampleProject());

        TreeNodeViewModel installRoot = vm.InstallationNodes[0];
        TreeNodeViewModel functionsRoot = vm.FunctionNodes[0];
        var populated = installRoot.Children.Where(c => c.Children.Count > 0).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(populated, Is.Not.Empty, "precondition: the sample project puts components in some localities");
            Assert.That(installRoot.IsExpanded, Is.True, "the locality root itself is open");
            Assert.That(installRoot.Children.Any(c => c.IsExpanded), Is.False,
                "no locality opens by itself in the Installation pane");
            Assert.That(functionsRoot.Children.Any(c => c.IsExpanded), Is.False,
                "nor in the Functions pane");
        });
    }
}
