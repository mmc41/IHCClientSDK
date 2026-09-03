using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsCheck;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A differential test for the change set the session publishes: every delta it reports is recomputed here from
    /// the two snapshots alone, by a second implementation that shares no code with the first, and the two must
    /// agree on every transition of a randomized sequence.
    ///
    /// <para>The interesting difference between the two is the optimization. The real diff skips any element that is
    /// REFERENCE-equal on both sides — the commit path shares untouched subtrees verbatim, and that skip is what
    /// makes an edit cost its own path instead of a full-tree walk. The recomputation here has no such shortcut: it
    /// compares every element on both sides by content. So the two agree only while sharing genuinely implies
    /// equality, which fixed examples can assert for one shape of edit at a time but not across sequences where
    /// commits, undos and rollbacks interleave and snapshots get reused.</para>
    ///
    /// <para><see cref="ProjectChangeSetTests"/> covers one example per category; this covers the categories
    /// together, over transitions produced by real commands rather than hand-built pairs of trees.</para>
    ///
    /// <para><b>What this deliberately does NOT cover: the id-less roll-up.</b> Measured across all 27 oracle
    /// projects, the only id-less elements that occur at all are the root <c>utcs_project</c> and its four direct
    /// children <c>modified</c>, <c>project_info</c>, <c>customer_info</c> and <c>installer_info</c> — every one of
    /// them attribute-only leaves under a root that has no id either. So no real project has an id-less element
    /// beneath an id-bearing ancestor, and the roll-up rule cannot fire; a recomputation that ignores id-less
    /// children entirely still agrees with the engine on every transition here (verified by mutation). The rule
    /// stays right to implement, and it IS covered — by
    /// <see cref="ProjectChangeSetTests.NestedIdlessChange_RollsUpToTheIdBearingAncestor"/>, which has to build
    /// that nesting by hand precisely because no project contains it. It simply is not something this test can
    /// reach through real commands.</para>
    /// </summary>
    public class ChangeSetDifferentialTests
    {
        private const string BaseProject = "testdata/projects/project3-KompleksWired.vis";

        private static ProjectAppService App => new(TestSetup.Settings);

        // ----- the independent recomputation -----

        private sealed record Delta(
            HashSet<ElementId> Added, HashSet<ElementId> Removed, HashSet<ElementId> Changed,
            HashSet<ElementId> ChildListChanged, bool MetadataChanged);

        private static readonly string[] MetadataBlocks = { "project_info", "customer_info", "installer_info" };

        /// <summary>
        /// The whole delta, computed from scratch: id-keyed set differences for added/removed, and for every id
        /// present on both sides a content comparison of the element itself plus its id-LESS subtree (an id-bearing
        /// descendant reports for itself, so the walk stops there) and of its sequence of id-bearing child ids.
        /// </summary>
        private static Delta Recompute(Project old, Project updated)
        {
            Dictionary<ElementId, ProjectElement> before = Map(old.Root);
            Dictionary<ElementId, ProjectElement> after = Map(updated.Root);

            var changed = new HashSet<ElementId>();
            var childListChanged = new HashSet<ElementId>();
            foreach ((ElementId id, ProjectElement a) in before)
            {
                if (!after.TryGetValue(id, out ProjectElement? b))
                {
                    continue;
                }
                if (Signature(a) != Signature(b))
                {
                    changed.Add(id);
                }
                if (!ChildIds(a).SequenceEqual(ChildIds(b)))
                {
                    childListChanged.Add(id);
                }
            }

            return new Delta(
                after.Keys.Where(id => !before.ContainsKey(id)).ToHashSet(),
                before.Keys.Where(id => !after.ContainsKey(id)).ToHashSet(),
                changed,
                childListChanged,
                MetadataBlocks.Any(tag => MetadataDiffers(old, updated, tag)));
        }

        private static bool MetadataDiffers(Project old, Project updated, string tag)
        {
            ProjectElement? a = old.Root.Children.FirstOrDefault(c => c.Tag == tag);
            ProjectElement? b = updated.Root.Children.FirstOrDefault(c => c.Tag == tag);
            return a is null || b is null ? a is not null || b is not null : Signature(a) != Signature(b);
        }

        private static Dictionary<ElementId, ProjectElement> Map(ProjectElement root)
        {
            var map = new Dictionary<ElementId, ProjectElement>();
            var pending = new Stack<ProjectElement>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                ProjectElement element = pending.Pop();
                if (element.Id is { } id)
                {
                    map.TryAdd(id, element);   // first wins, so the walk must be in document order
                }
                // Pushed in reverse so they pop in document order: should an id ever appear twice, first-wins
                // must resolve to the same element the engine kept, or the two would disagree over a tie rather
                // than over a change.
                for (int i = element.Children.Length - 1; i >= 0; i--)
                {
                    pending.Push(element.Children[i]);
                }
            }
            return map;
        }

        /// <summary>
        /// A textual rendering of the element's own tag and attributes plus its id-less subtree, so any difference
        /// in an attribute's name, value or position shows up as a different string. Every part is length-prefixed:
        /// no attribute value can forge a boundary, whatever punctuation it happens to contain.
        /// </summary>
        private static string Signature(ProjectElement element)
        {
            var text = new StringBuilder();
            Append(element);
            return text.ToString();

            void Append(ProjectElement node)
            {
                Part(node.Tag);
                Part(node.Attrs.Length.ToString(CultureInfo.InvariantCulture));
                foreach ((string name, string value) in node.Attrs)
                {
                    Part(name);
                    Part(value);
                }
                foreach (ProjectElement child in node.Children.Where(c => c.Id is null))
                {
                    Append(child);
                }
                text.Append("/|");
            }

            void Part(string value) =>
                text.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
        }

        private static List<ElementId> ChildIds(ProjectElement element) =>
            element.Children.Where(c => c.Id is not null).Select(c => c.Id!.Value).ToList();

        // ----- randomized transitions -----

        private abstract record Op;
        private sealed record AddOp(string Name) : Op;
        private sealed record RenameOp(int Pick, string Name) : Op;
        private sealed record DeleteOp(int Pick) : Op;
        private sealed record ReorderOp(int Pick, int Position) : Op;
        private sealed record ProjectInfoOp(string Text) : Op;
        private sealed record AddTextOp(string Text) : Op;
        private sealed record UndoOp : Op;
        private sealed record RedoOp : Op;
        private sealed record RollbackOp : Op;

        // This law's own operation set: it needs the two content edits (project info and a text node) that
        // make two change sets differ in a way structure alone would not show. Sharing one model across the
        // laws would force those on every other law's state space.
        private static readonly Gen<Op> AnyOp = Gen.OneOf(
            CsCheckValues.Name.Select(n => (Op)new AddOp(n)),
            Gen.Select(CsCheckValues.Pick, CsCheckValues.Name, (p, n) => (Op)new RenameOp(p, n)),
            CsCheckValues.Pick.Select(p => (Op)new DeleteOp(p)),
            Gen.Select(CsCheckValues.Pick, CsCheckValues.Pick, (p, pos) => (Op)new ReorderOp(p, pos)),
            CsCheckValues.Name.Select(t => (Op)new ProjectInfoOp(t)),
            CsCheckValues.Name.Select(t => (Op)new AddTextOp(t)),
            Gen.Const((Op)new UndoOp()),
            Gen.Const((Op)new RedoOp()),
            Gen.Const((Op)new RollbackOp()));

        private static readonly Gen<Op[]> Sequence = AnyOp.Array[1, 10];

        private static ProjectCommand? EditCommand(Op op, Project project)
        {
            IReadOnlyList<ProjectElement> groups = project.Groups;
            return op switch
            {
                AddOp add => new AddLocality(add.Name),
                RenameOp r when groups.Count > 0 =>
                    new RenameLocality(groups[r.Pick % groups.Count].Id!.Value, r.Name, string.Empty),
                DeleteOp d when groups.Count > 0 => new DeleteLocality(groups[d.Pick % groups.Count].Id!.Value),
                ReorderOp ro when groups.Count > 0 =>
                    new ReorderNode(groups[ro.Pick % groups.Count].Id!.Value, ro.Position % groups.Count),
                ProjectInfoOp info => new UpdateProjectInfo(
                    ProjectInfoData.Empty with { Description = info.Text, Programmer = info.Text }),
                AddTextOp text => new AddUserText(text.Text, TableExists: true),
                _ => null,
            };
        }

        /// <summary>Which categories the run actually produced. A differential test over transitions that all
        /// report "nothing changed" agrees perfectly and proves nothing.</summary>
        private sealed class Reached
        {
            public int Transitions;
            public int Added;
            public int Removed;
            public int Changed;
            public int ChildListChanged;
            public int Metadata;
        }

        private static string? CompareDelta(ProjectChangeSet published, Project before, Project after, Reached reached)
        {
            Delta expected = Recompute(before, after);
            reached.Transitions++;
            reached.Added += expected.Added.Count > 0 ? 1 : 0;
            reached.Removed += expected.Removed.Count > 0 ? 1 : 0;
            reached.Changed += expected.Changed.Count > 0 ? 1 : 0;
            reached.ChildListChanged += expected.ChildListChanged.Count > 0 ? 1 : 0;
            reached.Metadata += expected.MetadataChanged ? 1 : 0;

            if (!published.Added.SetEquals(expected.Added))
            {
                return $"Added: published [{Ids(published.Added)}], recomputed [{Ids(expected.Added)}]";
            }
            if (!published.Removed.SetEquals(expected.Removed))
            {
                return $"Removed: published [{Ids(published.Removed)}], recomputed [{Ids(expected.Removed)}]";
            }
            if (!published.Changed.SetEquals(expected.Changed))
            {
                return $"Changed: published [{Ids(published.Changed)}], recomputed [{Ids(expected.Changed)}]";
            }
            if (!published.ChildListChanged.SetEquals(expected.ChildListChanged))
            {
                return $"ChildListChanged: published [{Ids(published.ChildListChanged)}], "
                       + $"recomputed [{Ids(expected.ChildListChanged)}]";
            }
            if (published.MetadataChanged != expected.MetadataChanged)
            {
                return $"MetadataChanged: published {published.MetadataChanged}, recomputed {expected.MetadataChanged}";
            }
            return null;
        }

        private static string Ids(IEnumerable<ElementId> ids) => string.Join(",", ids.Select(id => id.ToToken()));

        [Test]
        public async Task PublishedChangeSet_MatchesAFullRecomputation_OverRandomizedSequences()
        {
            Project opened = await App.Load(BaseProject);
            var reached = new Reached();

            Sequence.Sample(ops =>
            {
                var session = new ProjectDocumentSession();
                session.Open(opened);

                foreach (Op op in ops)
                {
                    Project before = session.Current!;
                    EditOutcome? outcome = op switch
                    {
                        UndoOp => session.Undo(),
                        RedoOp => session.Redo(),
                        RollbackOp => session.Rollback(),
                        _ => EditCommand(op, before) is { } command ? session.Apply(command) : null,
                    };
                    if (outcome?.Changes is not { } published)
                    {
                        continue;   // refused, no-op, or an operation this state offered nothing for
                    }
                    if (CompareDelta(published, before, session.Current!, reached) is { } mismatch)
                    {
                        throw new AssertionException($"{op.GetType().Name}: {mismatch}");
                    }
                }
                return true;
            }, iter: 150, threads: 1);

            Assert.Multiple(() =>
            {
                Assert.That(reached.Transitions, Is.GreaterThan(0), "no transition published a change set at all");
                Assert.That(reached.Added, Is.GreaterThan(0), "no transition ever added an element");
                Assert.That(reached.Removed, Is.GreaterThan(0), "no transition ever removed an element");
                Assert.That(reached.Changed, Is.GreaterThan(0), "no transition ever changed an element in place");
                Assert.That(reached.ChildListChanged, Is.GreaterThan(0), "no transition ever changed a child list");
                Assert.That(reached.Metadata, Is.GreaterThan(0), "no transition ever touched a root metadata block");
            });
        }
    }
}
