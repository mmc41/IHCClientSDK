using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T004 — extends the T003 central locked-ancestor authorization to the IN-PLACE mutations (PG-2, finding #2):
    /// the AND/OR condition-logic toggle, save-current-value, log-mark, enum-state edit and the function-block rename
    /// are each refused when their target lies at/within a <c>locked="yes"</c> block, so no attribute edit reaches a
    /// locked block's internals either. The session commands surface a clean refusal via <c>Evaluate</c>; the
    /// enum-state edit is withdrawn at its gateway (its entry-point variable is the locked-block element — the enum
    /// TYPE it would edit is project-global). Oracles: the locked <c>AutoProof</c> block (project2) for the output
    /// rename/backup cases, and project3's locked library blocks (which carry real condition groups and enum
    /// variables) for the logic-toggle and enum-state cases.
    /// </summary>
    public class LockedBlockInPlaceGuardTests
    {
        private static IhcSettings Settings => TestSetup.Settings;
        private static ProjectAppService App => new(Settings);

        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectElement Fb(Project p, string name) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);

        // A locked library block that carries both a conditions group and an enum variable (project3's PIR block).
        private static ProjectElement RichLockedBlock(Project p) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("locked") == "yes"
                && e.Descendants().Any(d => d.Tag == "conditions")
                && e.Descendants().Any(d => d.Tag == "resource_enum"));

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        // ---- each in-place command refused when its target is inside a locked block ----

        [Test]
        public async Task SetConditionsLogic_OnLockedBlockConditions_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId conditions = RichLockedBlock(project).Descendants().First(e => e.Tag == "conditions").Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new SetConditionsLogic(conditions, Or: true));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("locked function block"));
            });
        }

        [Test]
        public async Task SetOutputBackup_OnLockedBlockOutput_IsRefused()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ElementId output = Fb(project, "AutoProof").FindChild("outputs")!
                .ChildrenOrEmpty().First(e => e.Tag == "resource_output").Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new SetOutputBackup(output, Save: true));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task RenameLocality_OnLockedFunctionBlock_IsRefused()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ElementId lockedBlock = Fb(project, "AutoProof").Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new RenameLocality(lockedBlock, "Renamed", string.Empty));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task UpdateEnumStates_ForLockedBlockEnumVariable_IsWithdrawn()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId enumVariable = RichLockedBlock(project).Descendants().First(e => e.Tag == "resource_enum").Id!.Value;

            UpdateEnumStates? command = App.Commands.UpdateEnumStates(project, enumVariable, new[] { "BrandNewState" });

            Assert.That(command, Is.Null, "the enum-state edit is withdrawn when its entry-point variable is locked");
        }

        [Test]
        public async Task ToggleLogMark_Engine_OnLockedBlockElement_IsRefused()
        {
            // No log row lives inside a function block (log rows are product-scoped), so the guard is exercised at the
            // engine, where it refuses any locked-block target before the Logning-shape check.
            Project project = await Load("project2-CustomBlock.vis");
            ProjectEditor editor = project.Edit();
            ElementId inLocked = Fb(project, "AutoProof").FindChild("outputs")!.ChildrenOrEmpty().First().Id!.Value;

            var ex = Assert.Throws<InvalidOperationException>(() => editor.ToggleLogMark(inLocked));
            Assert.That(ex!.Message, Does.Contain("locked function block"));
        }

        // ---- no over-reach: the same edits succeed off a locked block ----

        [Test]
        public async Task RenameLocality_OnALocality_IsAllowed()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ElementId locality = project.Groups.First().Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new RenameLocality(locality, "Renamed room", string.Empty));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
        }

        [Test]
        public async Task SetConditionsLogic_AfterUnlockingTheBlock_IsAllowed()
        {
            Project loaded = await Load("project3-KompleksWired.vis");
            ProjectElement block = RichLockedBlock(loaded);
            ElementId conditions = block.Descendants().First(e => e.Tag == "conditions").Id!.Value;
            ProjectEditor editor = loaded.Edit();
            editor.FunctionBlock(block.Id!.Value).Unlock();
            Project unlocked = editor.ToProject();

            EditOutcome outcome = Session(unlocked).Apply(new SetConditionsLogic(conditions, Or: true));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed),
                "once unlocked, the same in-place edit is permitted");
        }
    }
}
