using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace safe_visual_tests;

/// <summary>US-050: the read-only data-line module map — every input and output data line is listed, each
/// carrying the module documented on it; the view mutates nothing.</summary>
public class ModuleMapTests
{
    [Test]
    public async Task ModuleMap_EmptyProject_ListsEveryLineWithNoneInUse()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var map = harness.Session.GetDatalineModuleMap();

        Assert.Multiple(() =>
        {
            Assert.That(map.InputModules, Has.Length.EqualTo(8));
            Assert.That(map.OutputModules, Has.Length.EqualTo(16));
            Assert.That(map.InputModules.Concat(map.OutputModules).Any(m => m.InUse), Is.False);
        });
    }

    /// <summary>End to end through the shell, on the fixture both apps have open: the documented modules reach
    /// the view on their own data lines, and the untouched lines are still listed as free.</summary>
    [Test]
    public async Task ModuleMap_DocumentedModules_ReachTheViewOnTheirDataLines()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "project5-Dokumentation.vis"));

        var beforeProject = harness.Session.Current;
        var map = harness.Session.GetDatalineModuleMap();

        Assert.Multiple(() =>
        {
            Assert.That(map.InputModules.Where(m => m.InUse).Select(m => m.DataLine), Is.EqualTo(new[] { 1, 2, 8 }));
            Assert.That(map.InputModules[0].ModuleType, Is.EqualTo("Input 24/3"));
            Assert.That(map.InputModules[0].Location, Is.EqualTo("I sidetavle"));
            Assert.That(map.InputModules[0].Description, Is.EqualTo("Sensorer, lavt forbrug"));
            Assert.That(map.OutputModules.Where(m => m.InUse).Select(m => m.DataLine), Is.EqualTo(new[] { 1, 15 }));
            Assert.That(map.InputModules[2].InUse, Is.False, "line 3 carries no module and is still listed");
            Assert.That(harness.Session.Current, Is.SameAs(beforeProject), "reading the map mutates nothing");
        });
    }

    [Test]
    public async Task ModuleMapCommand_OpensTheReadOnlyView()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.ModuleMapCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ShowModuleMapCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastModuleMap, Is.Not.Null);
        });
    }
}
