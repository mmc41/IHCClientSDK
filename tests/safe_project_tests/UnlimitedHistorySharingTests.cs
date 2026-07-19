using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W4-4: history is unlimited now that a committed snapshot path-copies only the subtrees it changed
    /// (W4-3). The deterministic gate is NOT a memory threshold but the structural-sharing invariant that makes an
    /// unbounded history affordable: across consecutive history entries the untouched sibling subtrees are shared by
    /// reference, so a history entry costs its changed path — not a full tree copy — and undo across the whole history
    /// still restores exactly.
    /// </summary>
    public class UnlimitedHistorySharingTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        [Test]
        public async Task History_IsUnlimited_SharesUnchangedSubtreesAcrossEntries_AndUndoRestoresExactly()
        {
            Project baseProject = await App.Load("testdata/projects/project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(baseProject);

            // Rename each locality in turn, capturing the snapshot after each commit. Every edit changes exactly one
            // locality's subtree, so the next locality is untouched between consecutive snapshots.
            int localityCount = baseProject.Groups.Count;
            var snapshots = new List<Project> { session.Current! };
            for (int i = 0; i < localityCount; i++)
            {
                ElementId id = session.Current!.Groups[i].Id!.Value;
                session.Apply(new RenameLocality(id, $"Room-{i}", string.Empty));
                snapshots.Add(session.Current!);
            }

            Assert.Multiple(() =>
            {
                Assert.That(session.History.Cap, Is.Null, "the session history is unlimited (no cap)");
                Assert.That(session.CanUndo, Is.True);

                // (a)+(b): each history step shares the untouched sibling locality's subtree by reference — the entry
                // retains only its changed path, not a full tree copy.
                for (int i = 0; i < localityCount - 1; i++)
                {
                    ElementId untouchedId = snapshots[i].Groups[i + 1].Id!.Value;
                    ProjectElement before = snapshots[i].Groups.First(g => g.Id == untouchedId);
                    ProjectElement after = snapshots[i + 1].Groups.First(g => g.Id == untouchedId);
                    Assert.That(ReferenceEquals(before, after), Is.True,
                        $"history step {i}->{i + 1} shares the untouched locality {untouchedId.ToToken()} by reference");
                }
            });

            // Undo across the FULL history restores the base project exactly.
            for (int i = 0; i < localityCount; i++)
            {
                session.Undo();
            }
            Assert.Multiple(() =>
            {
                Assert.That(session.Current!.Equals(baseProject), Is.True, "undo across the full history restores the base project exactly");
                Assert.That(session.CanUndo, Is.False, "the history is fully unwound");
            });
        }
    }
}
