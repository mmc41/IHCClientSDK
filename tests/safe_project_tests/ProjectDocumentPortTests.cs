using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// crudarch T004 (proposal §3.1, D01): the <see cref="IProjectDocument"/> port — the interactive door
    /// returned by <see cref="ProjectAppService.OpenDocument"/> — driven purely through the interface:
    /// apply/undo/redo/dirty/labels/version behave as the session tests establish, undo AND redo outcomes
    /// carry non-null change sets with the right Origin, and the HistoryPolicy passes through.
    /// </summary>
    public class ProjectDocumentPortTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task OpenDocument_PortSurface_ApplyDirtyLabelsVersionBehave()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");

            IProjectDocument document = app.OpenDocument(project);

            Assert.Multiple(() =>
            {
                Assert.That(document.Current, Is.SameAs(project), "the opened snapshot is the given project");
                Assert.That(document.IsDirty, Is.False, "opened clean");
                Assert.That(document.CanUndo, Is.False);
                Assert.That(document.CanRedo, Is.False);
                Assert.That(document.UndoLabel, Is.Null);
                Assert.That(document.RedoLabel, Is.Null);
            });
            int openedVersion = document.Version;

            EditOutcome applied = document.Apply(app.Commands.AddLocality(document.Current!, "Port room"));

            Assert.Multiple(() =>
            {
                Assert.That(applied.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(document.Version, Is.GreaterThan(openedVersion), "apply bumps the version");
                Assert.That(document.IsDirty, Is.True);
                Assert.That(document.CanUndo, Is.True);
                Assert.That(document.UndoLabel, Is.EqualTo(applied.Label), "the port surfaces the action-named label");
            });

            int versionBeforeSave = document.Version;
            document.MarkSaved(document.Current!);
            Assert.Multiple(() =>
            {
                Assert.That(document.IsDirty, Is.False, "marking the current snapshot saved clears dirty");
                Assert.That(document.Version, Is.EqualTo(versionBeforeSave), "MarkSaved never bumps the version");
            });
        }

        [Test]
        public async Task UndoAndRedo_OutcomesCarryChangeSets_WithUndoRedoOrigins()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            IProjectDocument document = app.OpenDocument(project);
            document.Apply(app.Commands.AddLocality(document.Current!, "Port room"));

            EditOutcome undone = document.Undo();

            Assert.Multiple(() =>
            {
                Assert.That(undone.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(undone.Changes, Is.Not.Null, "the undo outcome carries its change set (G3)");
                Assert.That(undone.Changes!.Origin, Is.EqualTo("undo"));
                Assert.That(document.CanRedo, Is.True);
                Assert.That(document.RedoLabel, Is.EqualTo(undone.Label));
            });

            EditOutcome redone = document.Redo();

            Assert.Multiple(() =>
            {
                Assert.That(redone.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(redone.Changes, Is.Not.Null, "the redo outcome carries its change set (G3)");
                Assert.That(redone.Changes!.Origin, Is.EqualTo("redo"));
                Assert.That(document.CanRedo, Is.False, "redo consumed the redone entry");
            });
        }

        [Test]
        public async Task CanReorderNode_PortAnswerMatchesGatewayQuery_AcrossCases()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            IProjectDocument document = app.OpenDocument(project);

            ElementId g0 = project.Groups.First().Id!.Value;
            ElementId g1 = project.Groups.Skip(1).First().Id!.Value;
            ElementId childOfGroup = project.Groups.First(g => g.ChildrenOrEmpty().Any(c => c.Id is not null))
                .ChildrenOrEmpty().First(c => c.Id is not null).Id!.Value;
            ElementId product = project.Root.Descendants()
                .First(e => e.Tag.StartsWith("product_", System.StringComparison.Ordinal) && e.Id is not null).Id!.Value;
            // Same tag under DIFFERENT parents — reorder must refuse across containers.
            ElementId[]? crossParentPair = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null && project.FindParent(e.Id!.Value)?.Id is not null)
                .GroupBy(e => e.Tag)
                .Select(g => g.GroupBy(e => project.FindParent(e.Id!.Value)!.Id!.Value)
                    .Select(p => p.First().Id!.Value).Take(2).ToArray())
                .FirstOrDefault(pair => pair.Length == 2);

            Assert.Multiple(() =>
            {
                AssertCase(g0, g1, true, "same-tag siblings reorder");
                AssertCase(g0, g0, false, "a self-pair never reorders");
                AssertCase(g0, childOfGroup, false, "a group and its child are no sibling pair");
                AssertCase(g0, product, false, "cross-tag, cross-parent pair refuses");
                if (crossParentPair is [var a, var b])
                {
                    AssertCase(a, b, false, "same tag under different parents refuses");
                }
            });

            void AssertCase(ElementId dragged, ElementId target, bool expected, string label)
            {
                bool port = document.CanReorderNode(dragged, target);
                Assert.That(port, Is.EqualTo(app.Commands.CanReorderNode(project, dragged, target)),
                    $"port/gateway parity: {label}");
                Assert.That(port, Is.EqualTo(expected), label);
            }
        }

        // review F02: the delta-move gate probe. It must answer exactly "the gateway would mint a command AND that
        // command is applicable" — the conjunction the menu gate used to spell out itself — at the list boundaries,
        // for a no-op delta, and for an unknown id.
        [Test]
        public async Task CanReorder_PortAnswerMatchesGatewayFactoryPlusVerdict_AcrossCases()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");
            IProjectDocument document = app.OpenDocument(project);

            ElementId first = project.Groups.First().Id!.Value;
            ElementId middle = project.Groups.Skip(1).First().Id!.Value;
            ElementId last = project.Groups.Last().Id!.Value;

            Assert.Multiple(() =>
            {
                AssertCase(first, -1, false, "the first sibling cannot move up");
                AssertCase(first, +1, true, "…but it can move down");
                AssertCase(middle, -1, true, "a middle sibling moves both ways");
                AssertCase(middle, +1, true);
                AssertCase(last, -1, true, "the last sibling can move up");
                AssertCase(last, +1, false, "…but not down");
                AssertCase(middle, 0, false, "a zero move is a no-op, not a move");
                Assert.That(document.CanReorder(new ElementId(0xFFFFFF, 0xFF), -1), Is.False,
                    "an unknown id (the largest packed id) never reorders");
            });

            void AssertCase(ElementId id, int delta, bool expected, string label = "")
            {
                bool port = document.CanReorder(id, delta);
                Assert.That(port,
                    Is.EqualTo(app.Commands.ReorderNode(project, id, delta) is { } command
                               && app.CanApply(project, command).Ok),
                    $"port/gateway parity: {label} (delta {delta})");
                Assert.That(port, Is.EqualTo(expected), label);
            }
        }

        [Test]
        public async Task OpenDocument_HistoryPolicyPassesThrough()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");

            IProjectDocument document = app.OpenDocument(project, new HistoryPolicy(1));
            document.Apply(app.Commands.AddLocality(document.Current!, "First"));
            document.Apply(app.Commands.AddLocality(document.Current!, "Second"));
            document.Undo();

            Assert.That(document.CanUndo, Is.False,
                "the cap-1 policy kept only the newest history entry, so one undo drains it");
        }

        // review F04: the factory expresses the WHOLE open, so an interactive caller opening a recovered project
        // never has to re-open it (a second index build) and it is never momentarily reported clean.
        [Test]
        public async Task OpenDocument_StartCleanPassesThrough()
        {
            ProjectAppService app = App;
            Project project = await Load("project3-KompleksWired.vis");

            Assert.Multiple(() =>
            {
                Assert.That(app.OpenDocument(project).IsDirty, Is.False,
                    "a plain open makes the opened snapshot the save point");
                Assert.That(app.OpenDocument(project, startClean: false).IsDirty, Is.True,
                    "a recovered project has no clean state to return to — it opens dirty");
            });
        }
    }
}
