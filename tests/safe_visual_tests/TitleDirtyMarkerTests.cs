using System.Threading.Tasks;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// crudarch T019 (D07, U-BP-06): the window title carries the dirty bullet — "&lt;name&gt;• - IHC OpenVisual"
/// while unsaved changes exist, plain when clean. Save itself stays always-enabled (D07); the marker is the
/// unsaved-state UI. Dirty is the document's reference-computed flag, so undoing back to the save point
/// clears the bullet without saving.
/// </summary>
public class TitleDirtyMarkerTests : AvaloniaTestBase
{
    [Test]
    public async Task Title_TracksDirtyBullet_AcrossEditUndoSaveTransitions()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Assert.That(vm.Title, Is.EqualTo("Uden navn - IHC OpenVisual"), "a clean open shows no bullet");

        await harness.Session.AddLocalityAsync();
        Assert.That(vm.Title, Is.EqualTo("Uden navn• - IHC OpenVisual"), "an edit sets the bullet (D07)");

        await harness.Session.UndoAsync();
        Assert.That(vm.Title, Is.EqualTo("Uden navn - IHC OpenVisual"),
            "undoing back to the save point clears the bullet without saving");

        await harness.Session.RedoAsync();
        harness.Dialogs.SavePath = harness.TempPath("titled.vis");
        await harness.Session.SaveAsAsync();
        Assert.That(vm.Title, Is.EqualTo("titled.vis - IHC OpenVisual"),
            "a save names the file and clears the bullet");

        await harness.Session.AddLocalityAsync();
        Assert.That(vm.Title, Is.EqualTo("titled.vis• - IHC OpenVisual"), "a new edit re-marks the named document");
    }
}
