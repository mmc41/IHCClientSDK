using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-8 — <see cref="FunctionBlockRef.Unlock"/> (US-020 "Oplås"): demote a block loaded with
    /// <c>locked="yes"</c> to an editable custom block by clearing the flag to its default, so the canonicalizer
    /// omits it on save. Oracle: the locked <c>AutoProof</c> block in <c>project2-CustomBlock.vis:451</c>.
    /// <para>These cases cover the <c>locked</c> flag only. Unlock is NOT the inverse of
    /// <see cref="FunctionBlockRef.Locked"/> — it also discards the library identity and re-stamps ownership
    /// (including the icon, which uxparity S-20 measured as part of the FILE transform, not a GUI concern).
    /// That half is covered by <c>UnlockFunctionBlockParityTests</c>.</para>
    /// </summary>
    public class FunctionBlockUnlockTests
    {
        private const string Oracle = "project2-CustomBlock.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static FunctionBlockRef Block(ProjectEditor editor, Project project, string name)
        {
            ProjectElement fb = project.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);
            string groupName = project.FindParent(fb.Id!.Value)!.GetAttribute("name")!;
            return editor.Group(groupName).FunctionBlock(name);
        }

        [Test]
        public async Task Unlock_DemotesLoadedLockedBlock_OmittingTheLockedFlag()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectElement locked = project.Root.Descendants().First(e => e.GetAttribute("name") == "AutoProof");
            Assert.That(locked.GetAttribute("locked"), Is.EqualTo("yes"), "precondition: the block is loaded locked");

            ProjectEditor editor = project.Edit();
            Block(editor, project, "AutoProof").Unlock("Test Installer", new DateOnly(2026, 1, 1));
            Project built = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(built.FindById(locked.Id!.Value)!.GetAttribute("locked"), Is.Null,
                    "locked is cleared to its default and omitted — the block is now editable");
                Assert.That(app.Validate(built).IsValid, Is.True);
            });
        }

        [Test]
        public async Task Unlock_ReturnsHandle_ForChaining()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            FunctionBlockRef block = Block(editor, project, "AutoProof");

            Assert.That(block.Unlock("Test Installer", new DateOnly(2026, 1, 1)), Is.SameAs(block));
        }

        [Test]
        // Renamed for uxparity S-20: unlocking an already-editable block is NOT a no-op any more (it re-stamps
        // ownership), so only the claim that still holds is asserted — the lock flag itself round-trips.
        public async Task Locked_ThenUnlock_LeavesNoLockedAttribute()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            FunctionBlockRef custom = Block(editor, project, "Custom blok");   // not locked

            custom.Locked().Unlock("Test Installer", new DateOnly(2026, 1, 1));
            Project built = editor.ToProject();

            ProjectElement fb = built.Root.Descendants().First(e => e.GetAttribute("name") == "Custom blok");
            Assert.That(fb.GetAttribute("locked"), Is.Null, "lock then unlock leaves no locked attribute");
        }
    }
}
