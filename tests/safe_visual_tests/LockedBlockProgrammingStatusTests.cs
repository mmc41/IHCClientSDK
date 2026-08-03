using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// US-026 / A-27 — entering programming mode on a LOCKED (library) block must SAY that the program is read-only.
/// The block opens either way and authoring is withdrawn either way; what distinguishes the two is the status
/// message, and a user who is not told cannot know why the authoring commands are missing.
/// <para>
/// Regression origin (uxparity2 T007/V4, <c>tmp/uxparity2/verify/V4/notes.md</c>): the locked wording existed in
/// source but was unreachable, so the status text was byte-identical for a locked and an unlocked block. The cause
/// was reading lockedness from a tree node looked up AFTER the panes had been re-projected, instead of from the
/// model. These tests pin the OBSERVABLE outcome, so they stay valid however the value is derived.
/// </para>
/// </summary>
public class LockedBlockProgrammingStatusTests : AvaloniaTestBase
{
    // A catalog (library) block is locked; an empty block is not. Both are entered the same way, so the only
    // difference between the two tests below is the block's locked state.
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> ProgrammingModeOnAsync(bool locked)
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        if (locked)
        {
            var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
            await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        }
        else
        {
            await harness.Session.AddEmptyFunctionBlockAsync(loc);
        }
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        return (harness, vm);
    }

    [Test]
    public async Task EnterProgrammingMode_OnLockedBlock_StatusSaysReadOnly()
    {
        var (harness, vm) = await ProgrammingModeOnAsync(locked: true);
        using var _ = harness;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True, "a locked block still ENTERS programming mode (C3)");
            Assert.That(vm.IsProgrammingBlockLocked, Is.True, "the fixture block really is locked");
            Assert.That(vm.StatusText, Does.Contain("read-only"),
                "a locked block's program is view-only and the status bar must say so");
        });
    }

    [Test]
    public async Task EnterProgrammingMode_OnUnlockedBlock_StatusDoesNotSayReadOnly()
    {
        var (harness, vm) = await ProgrammingModeOnAsync(locked: false);
        using var _ = harness;

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True);
            Assert.That(vm.IsProgrammingBlockLocked, Is.False, "an empty block is editable");
            Assert.That(vm.StatusText, Does.Not.Contain("read-only"),
                "an editable block must NOT be described as read-only");
        });
    }

    // The two messages must actually DIFFER. Without this, both tests above would still pass if the locked wording
    // leaked into the unlocked case and the substring happened to match — and it is the identical-text symptom that
    // the live measurement actually found.
    [Test]
    public async Task LockedAndUnlocked_ProgrammingStatusTexts_Differ()
    {
        var (lockedHarness, lockedVm) = await ProgrammingModeOnAsync(locked: true);
        using var _ = lockedHarness;
        var (openHarness, openVm) = await ProgrammingModeOnAsync(locked: false);
        using var __ = openHarness;

        Assert.That(lockedVm.StatusText, Is.Not.EqualTo(openVm.StatusText),
            "the locked and unlocked programming-mode messages must not be the same string");
    }
}
