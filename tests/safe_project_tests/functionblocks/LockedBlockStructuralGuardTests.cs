using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T003 — the ONE central engine-level locked-ancestor authorization (PG-2, US-020/US-026, review finding #2):
    /// every STRUCTURAL mutation whose target lies at/within a <c>locked="yes"</c> function block's subtree is refused
    /// <b>whoever drives the editor</b> — direct engine calls throw, and the session commands surface a clean
    /// <see cref="EditStatus.Refused"/> via <c>Evaluate</c>. Covered families: insert variable / program-row / pin,
    /// <see cref="ProjectEditor.ReorderSubtree"/>, and <see cref="ProjectEditor.MoveSubtree"/> /
    /// <see cref="ProjectEditor.CopySubtree"/> whose target parent is inside the locked subtree. The guard checks the
    /// mutation's TARGET, so relocating or copying the whole locked block to an unlocked locality — and every edit on
    /// an unlocked block — stays allowed (no over-reach). Oracle: the locked <c>AutoProof</c> block and the unlocked
    /// <c>Custom blok</c>, both under the <c>Stue</c> locality of <c>project2-CustomBlock.vis</c>.
    /// </summary>
    public class LockedBlockStructuralGuardTests
    {
        private const string Oracle = "project2-CustomBlock.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() => new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static ProjectElement Fb(Project p, string name) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);

        private static ElementId SectionId(Project p, string block, string section) =>
            Fb(p, block).FindChild(section)!.Id!.Value;

        // ---- engine level: a direct editor call is refused (throws) whoever drives it ----

        [Test]
        public async Task Engine_InsertVariable_IntoLockedBlockSection_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId autoProof = Fb(project, "AutoProof").Id!.Value;

            var ex = Assert.Throws<InvalidOperationException>(
                () => editor.FunctionBlock(autoProof).AddInternalVariable("resource_flag", "New"));
            Assert.That(ex!.Message, Does.Contain("locked function block"));
        }

        [Test]
        public async Task Engine_InsertProgramRow_IntoLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId actions = Fb(project, "AutoProof").Descendants().First(e => e.Tag == "actions").Id!.Value;

            Assert.Throws<InvalidOperationException>(() => editor.AllocateChild(actions, "action"));
        }

        [Test]
        public async Task Engine_ReorderNode_InsideLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId output = Fb(project, "AutoProof").FindChild("outputs")!.ChildrenOrEmpty().First().Id!.Value;

            Assert.Throws<InvalidOperationException>(() => editor.ReorderSubtree(output, 0));
        }

        [Test]
        public async Task Engine_MoveNode_IntoLockedBlockSection_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId customBlok = Fb(project, "Custom blok").Id!.Value;
            ElementId lockedInputs = SectionId(project, "AutoProof", "inputs");

            Assert.Throws<InvalidOperationException>(() => editor.MoveSubtree(customBlok, lockedInputs));
        }

        [Test]
        public async Task Engine_CopyNode_IntoLockedBlockSection_IsRefused()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId customBlok = Fb(project, "Custom blok").Id!.Value;
            ElementId lockedInputs = SectionId(project, "AutoProof", "inputs");

            Assert.Throws<InvalidOperationException>(() => editor.CopySubtree(customBlok, lockedInputs));
        }

        // ---- session level: the command surfaces the refusal as a clean verdict (not a fault) ----

        [Test]
        public async Task Session_AddVariable_IntoLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            var svc = new ProjectAppService(Settings);
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId lockedInputs = SectionId(project, "AutoProof", "inputs");

            AddVariable? command = svc.Commands.AddVariable(session.Current!, lockedInputs, "resource_input", "New");
            Assert.That(command, Is.Not.Null);
            EditOutcome outcome = session.Apply(command!);

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("locked function block"));
            });
        }

        [Test]
        public async Task Session_ReorderNode_InsideLockedBlock_IsRefused()
        {
            Project project = await LoadOracle();
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId output = Fb(project, "AutoProof").FindChild("outputs")!.ChildrenOrEmpty().First().Id!.Value;

            EditOutcome outcome = session.Apply(new ReorderNode(output, 0));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        // ---- no over-reach: the guard is about the TARGET, not "anything touching a locked block" ----

        [Test]
        public async Task Engine_InsertVariable_IntoUnlockedBlock_IsAllowed()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId customBlok = Fb(project, "Custom blok").Id!.Value;

            Assert.DoesNotThrow(() => editor.FunctionBlock(customBlok).AddInput("New"));
        }

        [Test]
        public async Task Engine_MoveLockedBlock_ToAnUnlockedLocality_IsAllowed()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            ElementId autoProof = Fb(project, "AutoProof").Id!.Value;
            ElementId emptyLocality = project.Groups.Last().Id!.Value;   // an empty room, not locked

            Assert.DoesNotThrow(() => editor.MoveSubtree(autoProof, emptyLocality));
        }
    }
}
