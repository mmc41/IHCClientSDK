using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W4-3 (gate 2): the sharing-preserving commit. The full <c>safe_project_tests</c> oracle corpus is
    /// gate 1 — it pins that the canonical FORM is byte-unchanged (incremental ≡ full is byte-safe by construction:
    /// <c>Canonicalizer</c> returns the input only when it already equals the full canonical result). This adds the
    /// two directly-testable, W4-3-specific laws over RANDOMIZED command sequences:
    /// <list type="number">
    /// <item>the incrementally-committed project's bytes round-trip byte-identically (Load∘Save is a fixed point);</item>
    /// <item>a commit that changes one subtree SHARES the untouched subtrees by reference with the pre-commit project
    /// (the point of "sharing-preserving": path-copy only what changed).</item>
    /// </list>
    /// </summary>
    public class SharingPreservingCommitPropertyTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private const string BaseProject = "testdata/projects/project3-KompleksWired.vis";

        private abstract record Op;
        private sealed record AddOp(string Name) : Op;
        private sealed record RenameOp(int Pick, string Name) : Op;
        private sealed record DeleteOp(int Pick) : Op;
        private sealed record ReorderOp(int Pick, int Position) : Op;

        // This law's own operation set, and the smallest of them: sharing preservation is about what a commit
        // does to untouched subtrees, so history operations would add states the law says nothing about.
        private static readonly Gen<Op> AnyOp = Gen.OneOf(
            CsCheckValues.Name.Select(n => (Op)new AddOp(n)),
            Gen.Select(CsCheckValues.Pick, CsCheckValues.Name, (p, n) => (Op)new RenameOp(p, n)),
            CsCheckValues.Pick.Select(p => (Op)new DeleteOp(p)),
            Gen.Select(CsCheckValues.Pick, CsCheckValues.Pick, (p, pos) => (Op)new ReorderOp(p, pos)));

        private static readonly Gen<Op[]> CommandSequence = AnyOp.Array[0, 8];

        private static ProjectCommand? Interpret(Op op, Project project)
        {
            System.Collections.Generic.IReadOnlyList<ProjectElement> groups = project.Groups;
            return op switch
            {
                AddOp add => new AddLocality(add.Name),
                RenameOp r when groups.Count > 0 => new RenameLocality(groups[r.Pick % groups.Count].Id!.Value, r.Name, string.Empty),
                DeleteOp d when groups.Count > 0 => new DeleteLocality(groups[d.Pick % groups.Count].Id!.Value),
                ReorderOp ro when groups.Count > 0 => new ReorderNode(groups[ro.Pick % groups.Count].Id!.Value, ro.Position % groups.Count),
                _ => null,
            };
        }

        [Test]
        public async Task IncrementallyCommittedBytes_RoundTripByteIdentically_OverRandomSequences()
        {
            Project baseProject = await App.Load(BaseProject);
            // Drive each sequence, then reload the committed bytes and re-canonicalize (a fresh, cache-miss full
            // canonicalize): the incremental commit's bytes must be reproduced exactly.
            CommandSequence.Sample(ops =>
            {
                var session = new ProjectDocumentSession();
                session.Open(baseProject);
                foreach (Op op in ops)
                {
                    if (Interpret(op, session.Current!) is { } command)
                    {
                        session.Apply(command);
                    }
                }
                Project committed = session.Current!;
                byte[] incremental = ProjectSerializer.Serialize(committed);
                Project reloaded = App.Load(new MemoryStream(incremental)).GetAwaiter().GetResult();
                byte[] roundTripped = ProjectSerializer.Serialize(reloaded.Edit().ToProject());
                return incremental.AsSpan().SequenceEqual(roundTripped);
            }, iter: 100, threads: 1);
        }

        [Test]
        public async Task Commit_SharesUntouchedSiblingSubtrees_ByReference()
        {
            Project baseProject = await App.Load(BaseProject);
            var session = new ProjectDocumentSession();
            session.Open(baseProject);
            ProjectElement untouched = session.Current!.Groups[1];   // a locality the edit will not touch
            ElementId renamed = session.Current!.Groups[0].Id!.Value;

            session.Apply(new RenameLocality(renamed, "Renamed-XYZ", string.Empty));

            ProjectElement afterUntouched = session.Current!.Groups.First(g => g.Id == untouched.Id);
            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(afterUntouched, untouched), Is.True,
                    "a commit that renames one locality shares the untouched sibling locality's subtree by reference");
                Assert.That(session.Current!.Groups[0].GetAttribute("name"), Is.EqualTo("Renamed-XYZ"),
                    "the changed locality was re-materialized with the new name");
            });
        }
    }
}
