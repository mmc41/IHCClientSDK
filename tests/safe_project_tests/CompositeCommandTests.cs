using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-11: a <see cref="CompositeCommand"/> bundles several commands into one gesture. One
    /// <c>Apply</c> is one history entry (a single Undo reverses the whole bundle); if any part's legality check
    /// refuses, the whole gesture is Refused with nothing committed.
    /// </summary>
    public class CompositeCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        [Test]
        public async Task Composite_OfTwoParts_IsOneUndoStep()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            Project before = session.Current!;

            EditOutcome outcome = session.Apply(
                new CompositeCommand("Insert two localities", new AddLocality("Alpha"), new AddLocality("Beta")));
            bool couldUndoOnce = session.CanUndo;
            session.Undo();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(couldUndoOnce, Is.True, "the committed gesture pushed a history entry");
                Assert.That(session.CanUndo, Is.False, "the two parts collapsed into a single undo step");
                // Content-equal, not snapshot-equal: undo deliberately keeps the raised last_unique_id
                // (alignment F-10 — the allocator is monotonic across history, vendor-measured 2026-08-09).
                Assert.That(session.Current!.Groups.Count, Is.EqualTo(before.Groups.Count),
                    "one undo reverses the whole gesture — both localities gone");
                Assert.That(session.Current!.Root.WithAttribute("last_unique_id", "_0x0")
                        .Equals(before.Root.WithAttribute("last_unique_id", "_0x0")),
                    Is.True, "one undo reverses the whole gesture (modulo the kept allocator high-water)");
            });
        }

        [Test]
        public async Task Composite_WithRefusedPart_RefusesWholeGesture()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            Project before = session.Current!;
            var missing = new ElementId(0x7FFFFF, 1);   // a counter far beyond project3's live id range

            EditOutcome outcome = session.Apply(new CompositeCommand("Bad gesture",
                new AddLocality("Alpha"), new RenameLocality(missing, "x", "")));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused), "a refused part refuses the whole");
                Assert.That(session.CanUndo, Is.False, "nothing committed");
                Assert.That(ReferenceEquals(session.Current, before), Is.True,
                    "the first part's add did not apply — an all-or-nothing gesture");
            });
        }
    }
}
