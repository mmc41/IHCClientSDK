using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T010 / US-021 / PG-3a — Save-to-library transforms the in-project block into a locked library instance via the
    /// undoable <see cref="SaveFunctionBlockToLibrary"/> command: renamed, <c>master_*</c> stamped, library badge +
    /// note applied, <c>locked="yes"</c> — no re-insertion. One undo restores the prior unlocked block. The failure
    /// ordering (export FIRST) means a failed <c>.ifb</c> export leaves the project unmutated. Oracle: the UNLOCKED
    /// <c>Custom blok</c> of <c>project2-CustomBlock.vis</c>.
    /// </summary>
    public class SaveToLibraryTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static Task<Project> Load() => new ProjectAppService(Settings).Load("testdata/projects/project2-CustomBlock.vis");

        private static ProjectElement Fb(Project p, string name) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        [Test]
        public async Task SaveToLibrary_TransformsBlockInPlace_LockedRenamedAndStamped()
        {
            Project project = await Load();
            ElementId id = Fb(project, "Custom blok").Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new SaveFunctionBlockToLibrary(id, "MyLib", "Author", new DateOnly(2026, 7, 11), "A note"));

            ProjectElement after = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(after.GetAttribute("locked"), Is.EqualTo("yes"), "the block is locked in place");
                Assert.That(after.GetAttribute("name"), Is.EqualTo("MyLib"), "renamed to the saved name");
                Assert.That(after.GetAttribute("master_name"), Is.EqualTo("MyLib"));
                Assert.That(after.GetAttribute("master_programmer"), Is.EqualTo("Author"));
                Assert.That(after.GetAttribute("icon"), Is.EqualTo("_0x10"), "the user-library badge");
                Assert.That(after.GetAttribute("note"), Is.EqualTo("A note"));
            });
        }

        /// <summary>
        /// Saving to the library makes the in-project block YOUR library instance, so it stops claiming to be the
        /// Schneider block it came from: the three library-identity keys go, exactly as they do on unlock (S-20) and
        /// exactly as the exported <c>.ifb</c> already dropped them. Measured against IHC Visual's own save
        /// (uxparity S-22) — without this the saved project still advertises `master_type="1.1.01"` for a block whose
        /// name, author, date and contents are now the installer's.
        /// </summary>
        [Test]
        public async Task SaveToLibrary_DropsTheSourceLibraryIdentity()
        {
            // A different fixture: project2's blocks are hand-authored, so none of them carries a library identity to
            // drop. Project1's Kip block is a stock Schneider block, which is the case this is about.
            Project project = await new ProjectAppService(Settings).Load("testdata/projects/Project1-SimpelWired.vis");
            ProjectElement library = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("master_type") == "1.1.01");
            ElementId id = library.Id!.Value;
            ProjectDocumentSession session = Session(project);

            session.Apply(new SaveFunctionBlockToLibrary(id, "MyLib", "Author", new DateOnly(2026, 7, 11), null));

            ProjectElement after = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(after.GetAttribute("master_type"), Is.Null.Or.Empty);
                Assert.That(after.GetAttribute("master_version"), Is.Null.Or.Empty);
                Assert.That(after.GetAttribute("master_schneider_electric"), Is.Not.EqualTo("yes"));
                Assert.That(after.GetAttribute("master_name"), Is.EqualTo("MyLib"), "it is the new library block now");
            });
        }

        [Test]
        public async Task SaveToLibrary_OneUndo_RestoresThePriorUnlockedBlock()
        {
            Project project = await Load();
            ProjectElement before = Fb(project, "Custom blok");
            ElementId id = before.Id!.Value;
            string priorName = before.GetAttribute("name")!;
            string? priorLocked = before.GetAttribute("locked");   // unlocked: absent
            ProjectDocumentSession session = Session(project);

            session.Apply(new SaveFunctionBlockToLibrary(id, "MyLib", "Author", new DateOnly(2026, 7, 11), null));
            session.Undo();

            ProjectElement restored = session.Current!.FindById(id)!;
            Assert.Multiple(() =>
            {
                Assert.That(restored.GetAttribute("name"), Is.EqualTo(priorName), "one undo restores the name");
                Assert.That(restored.GetAttribute("locked"), Is.EqualTo(priorLocked), "one undo restores the unlocked state");
            });
        }

        [Test]
        public async Task SaveToLibrary_LockedTransform_SurvivesSaveReload()
        {
            Project project = await Load();
            ElementId id = Fb(project, "Custom blok").Id!.Value;
            ProjectDocumentSession session = Session(project);
            session.Apply(new SaveFunctionBlockToLibrary(id, "MyLib", "Author", new DateOnly(2026, 7, 11), null));

            using var ms = new MemoryStream();
            await new ProjectAppService(Settings).Save(session.Current!, ms);
            Project reloaded = ProjectReader.Read(ms.ToArray());

            Assert.That(reloaded.FindById(id)!.GetAttribute("locked"), Is.EqualTo("yes"),
                "the locked/stamped transform serialises into the .vis bytes");
        }

        [Test]
        public async Task SaveToLibrary_FailedExport_LeavesTheProjectUnmutated()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/projects/project2-CustomBlock.vis");
            ElementId id = Fb(project, "Custom blok").Id!.Value;
            using var closed = new MemoryStream();
            closed.Close();   // a failing .ifb sink

            Assert.Catch(() => app.SaveFunctionBlockToLibrary(project, id, closed, "Author", "MyLib"));

            Assert.That(project.FindById(id)!.GetAttribute("locked"), Is.Not.EqualTo("yes"),
                "the export runs before the transform, so a failed export leaves the block unmutated");
        }
    }
}
