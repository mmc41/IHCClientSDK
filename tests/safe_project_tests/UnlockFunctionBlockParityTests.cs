using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Unlocking a library function block must do to the file what IHC Visual does (uxparity S-20). Measured on
    /// `Project1-SimpelWired.vis`: unlocking is not just clearing <c>locked</c> — the block stops being a LIBRARY
    /// block and becomes the installer's own. The vendor drops the three library-identity keys
    /// (<c>master_schneider_electric</c>, <c>master_type</c>, <c>master_version</c>), keeps <c>master_name</c>,
    /// re-stamps <c>master_programmer</c>/<c>master_date_*</c> to the current user and today, and switches the icon
    /// from the locked-library glyph <c>_0xe</c> to <c>_0xf</c>. Name and note are left alone.
    ///
    /// <para>This is the same identity change the export path already performs
    /// (<c>FunctionBlockRef.ExportDefinition</c>), applied in place instead of to a copy.</para>
    /// </summary>
    public class UnlockFunctionBlockParityTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static async Task<(ProjectDocumentSession Session, ElementId Id)> UnlockedAsync()
        {
            Project project = await App.Load("testdata/projects/Project1-SimpelWired.vis");
            ProjectElement fb = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("master_type") == "1.1.01");
            var session = new ProjectDocumentSession();
            session.Open(project);
            session.Apply(new UnlockFunctionBlock(fb.Id!.Value, "Test Installer", new DateOnly(2026, 8, 1)));
            return (session, fb.Id!.Value);
        }

        [Test]
        public async Task Unlock_DropsTheLibraryIdentity_AndTakesOwnership()
        {
            (ProjectDocumentSession session, ElementId id) = await UnlockedAsync();

            ProjectElement block = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(block.GetAttribute("locked"), Is.Not.EqualTo("yes"));
                // No longer a library block: the three identity keys are gone…
                Assert.That(block.GetAttribute("master_schneider_electric"), Is.Not.EqualTo("yes"));
                Assert.That(block.GetAttribute("master_type"), Is.Null.Or.Empty);
                Assert.That(block.GetAttribute("master_version"), Is.Null.Or.Empty);
                // …but it remembers the name it came from, and is now stamped to whoever unlocked it.
                Assert.That(block.GetAttribute("master_name"), Is.EqualTo("Kip tænd sluk"));
                Assert.That(block.GetAttribute("master_programmer"), Is.EqualTo("Test Installer"));
                Assert.That(block.GetAttribute("master_date_year"), Is.EqualTo("2026"));
                Assert.That(block.GetAttribute("master_date_month"), Is.EqualTo("8"));
                Assert.That(block.GetAttribute("master_date_day"), Is.EqualTo("1"));
                Assert.That(block.GetAttribute("icon"), Is.EqualTo("_0xf"), "the unlocked-block glyph");
            });
        }

        /// <summary>The block keeps its own display name and note — unlocking is not a rename.</summary>
        [Test]
        public async Task Unlock_LeavesNameAndNoteAlone()
        {
            (ProjectDocumentSession session, ElementId id) = await UnlockedAsync();

            ProjectElement block = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(block.GetAttribute("name"), Is.EqualTo("1.1.01.e. Kip tænd sluk"));
                Assert.That(block.GetAttribute("note"), Does.StartWith("1.1.01.e. Kip tænd sluk"));
            });
        }

        /// <summary>One undo restores every stamp, not only the lock (E14 / W0-3 #5 generalised).</summary>
        [Test]
        public async Task Unlock_ThenUndo_RestoresTheWholeLibraryIdentity()
        {
            (ProjectDocumentSession session, ElementId id) = await UnlockedAsync();

            session.Undo();

            ProjectElement block = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(block.GetAttribute("locked"), Is.EqualTo("yes"));
                Assert.That(block.GetAttribute("master_type"), Is.EqualTo("1.1.01"));
                Assert.That(block.GetAttribute("master_version"), Is.EqualTo("e"));
                Assert.That(block.GetAttribute("master_schneider_electric"), Is.EqualTo("yes"));
                Assert.That(block.GetAttribute("master_programmer"), Does.StartWith("Schneider Electric"));
                Assert.That(block.GetAttribute("master_date_year"), Is.EqualTo("2017"));
                Assert.That(block.GetAttribute("icon"), Is.EqualTo("_0xe"));
            });
        }
    }
}
