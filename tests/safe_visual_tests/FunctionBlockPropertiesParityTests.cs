using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// A function block's Properties dialog must offer what IHC Visual's does (uxparity S-19). Measured against the
/// vendor on `Project1-SimpelWired.vis`: its <c>Funktionsblok egenskaber</c> has TWO groups —
///
/// <list type="bullet">
/// <item><c>Bruger egenskaber</c>: Navn and Note, both editable. Every block has these.</item>
/// <item><c>Oprindelige egenskaber</c>: Navn, Nummer, Version, Oprettet, Udviklet af — all read-only, and shown
/// only for a block stamped from the LIBRARY. A block authored from scratch (`Tom blok`) shows the first group
/// alone, which is what OpenVisual already did for every block.</item>
/// </list>
///
/// The provenance is in the file (<c>master_name</c>/<c>master_type</c>/<c>master_version</c>/
/// <c>master_date_*</c>/<c>master_programmer</c>), so this is data the project already carries and OpenVisual
/// simply did not surface.
/// </summary>
public class FunctionBlockPropertiesParityTests
{
    [Test]
    public async Task LibraryFunctionBlock_Properties_ShowsItsOrigin()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));
        var libraryBlock = vm.FunctionNodes[0].Children[0].Children[0];   // Stue / 1.1.01.e. Kip tænd sluk

        await vm.PropertiesCommand.ExecuteAsync(libraryBlock);

        Assert.That(harness.Dialogs.LastPropertiesOrigin, Is.EqualTo(
            new LibraryOrigin("Kip tænd sluk", "1.1.01", "e", "17/05/2017", "Schneider Electric\r\nCopyrights © 2009")));
    }

    [Test]
    public async Task BlockAuthoredFromScratch_Properties_ShowsNoOrigin()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[7].ElementId!.Value);

        await vm.PropertiesCommand.ExecuteAsync(vm.FunctionNodes[0].Children[7].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesOrigin, Is.Null,
            "a block with no library master has no provenance group");
    }

    /// <summary>
    /// Once unlocked, the block is the installer's own and the vendor's dialog drops the whole provenance group
    /// (uxparity S-20 measured 7 fields before, 2 after). The block still carries a <c>master_name</c> at that
    /// point, so "has a master name" is the wrong test for whether to show the group — it is the library identity
    /// (<c>master_type</c>) that decides.
    /// </summary>
    [Test]
    public async Task UnlockedFunctionBlock_Properties_ShowsNoOrigin()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));
        var libraryBlock = vm.FunctionNodes[0].Children[0].Children[0];
        await harness.Session.UnlockFunctionBlockAsync(libraryBlock.ElementId!.Value);

        await vm.PropertiesCommand.ExecuteAsync(vm.FunctionNodes[0].Children[0].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesOrigin, Is.Null,
            "unlocking takes ownership, so there is no library origin left to report");
    }

    /// <summary>
    /// Alignment F-24 (2026-08-11): a function block's dialog names its FIRST group too. The vendor's
    /// <c>Funktionsblok egenskaber</c> wraps Navn and Note in a group box captioned <c>Bruger egenskaber</c> — on a
    /// library block, an unlocked one and an empty one alike (measured on all three; the locked one's 19 controls
    /// carry both captions, the unlocked one's 7 carry this one).
    ///
    /// <para>OpenVisual captioned only the SECOND group, so a library block's dialog read "Navn, Note,
    /// <i>Oprindelige egenskaber</i>, …" — the provenance named, the fields above it belonging to an unnamed group.
    /// That is a divergence from the vendor and an internal inconsistency with its own sibling group; the caption
    /// is what tells a reader (and a screen reader) that those two fields are the ones they own.</para>
    ///
    /// <para>Gated on the node being a function block: the vendor's OTHER properties dialogs group differently —
    /// a locality's wraps each field in its own captioned box (<c>Navn</c>, <c>Note</c>), measured the same day —
    /// so an unconditional caption here would invent grouping the vendor does not have there.</para>
    /// </summary>
    [TestCase(true, "Bruger egenskaber")]
    [TestCase(false, null)]
    public async Task UserPropertiesGroupIsCaptioned_OnAFunctionBlockOnly(bool functionBlock, string? caption)
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);

        TreeNodeViewModel target = functionBlock
            ? vm.FunctionNodes[0].Children[0].Children[0]      // the block
            : vm.InstallationNodes[0].Children[0];             // a locality
        await vm.PropertiesCommand.ExecuteAsync(target);

        Assert.That(harness.Dialogs.LastPropertiesUserGroup, Is.EqualTo(caption));
    }

    /// <summary>
    /// The save-to-library dialog's affirmative button names the verb — the vendor's reads <c>Gem</c> (Save), not
    /// OK, because pressing it goes on to write a file (uxparity S-22). The ordinary properties dialog, which only
    /// edits in place, keeps OK.
    /// </summary>
    [Test]
    public async Task SaveFunctionBlockDialog_AffirmativeIsGem_NotOk()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        var block = vm.FunctionNodes[0].Children[0].Children[0];

        await vm.SaveFunctionBlockCommand.ExecuteAsync(block);

        Assert.That(harness.Dialogs.LastPropertiesAffirmative, Is.EqualTo("Gem"));
    }

    [Test]
    public async Task PropertiesDialog_AffirmativeStaysOk()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesAffirmative, Is.EqualTo("OK"));
    }

    /// <summary>A locality is not a function block and must not grow the group either.</summary>
    [Test]
    public async Task Locality_Properties_ShowsNoOrigin()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.PropertiesCommand.ExecuteAsync(vm.InstallationNodes[0].Children[0]);

        Assert.That(harness.Dialogs.LastPropertiesOrigin, Is.Null);
    }
}
