using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Projects;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The registered difference "support multiple instances": the reference application is one document at a time,
/// IHC OpenVisual is one project per window, several at once.
///
/// <para>The promise is not that two shells can be CONSTRUCTED — it is that they do not share state. A single
/// static document, a shared undo history or a process-wide "current project" would let two open windows corrupt
/// each other's work, and the damage would be invisible until the second one saved. So this pins independence at
/// the three places state could leak: the project itself, the dirty flag, and the undo history.</para>
/// </summary>
public class MultipleInstancesTests
{
    [Test]
    public async Task TwoShells_HoldIndependentProjects_HistoriesAndDirtyState()
    {
        using var first = ShellHarness.Create();
        using var second = ShellHarness.Create();
        MainWindowViewModel a = first.CreateViewModel();
        MainWindowViewModel b = second.CreateViewModel();
        await a.InitializeAsync();
        await b.InitializeAsync();
        int localitiesInB = second.Session.Current!.Groups.Count;

        await a.InsertLocalityCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(first.Session.Current!.Groups.Count, Is.EqualTo(localitiesInB + 1),
                "the edit lands in the window that made it");
            Assert.That(second.Session.Current!.Groups.Count, Is.EqualTo(localitiesInB),
                "and nowhere else — a shared document would show it here too");
            Assert.That(first.Session.IsDirty, Is.True);
            Assert.That(second.Session.IsDirty, Is.False,
                "the second window has unsaved changes it never made — it would prompt to save nothing");
            Assert.That(first.Session.CanUndo, Is.True);
            Assert.That(second.Session.CanUndo, Is.False,
                "a shared history would let one window undo the other's edit");
        });
    }

    /// <summary>Two windows on two DIFFERENT files, each saving its own — the case a shared "current project"
    /// would turn into one file overwriting the other.</summary>
    [Test]
    public async Task TwoShells_SaveTheirOwnFiles()
    {
        using var first = ShellHarness.Create();
        using var second = ShellHarness.Create();
        MainWindowViewModel a = first.CreateViewModel();
        MainWindowViewModel b = second.CreateViewModel();
        await a.InitializeAsync();
        await b.InitializeAsync();
        first.Dialogs.SavePath = first.TempPath("a.vis");
        second.Dialogs.SavePath = second.TempPath("b.vis");

        await a.InsertLocalityCommand.ExecuteAsync(null);
        await first.Session.SaveAsAsync();
        await second.Session.SaveAsAsync();

        Project reopenedB = await second.ProjectService.Load(second.TempPath("b.vis"));
        Assert.That(reopenedB.Groups.Count, Is.EqualTo(second.Session.Current!.Groups.Count),
            "the second window saved ITS project, not the first window's");
    }
}
