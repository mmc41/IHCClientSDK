using System.Linq;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T014 / US-031 / PG-6 — adding a case value to an ENUM-keyed case. The gateway used to no-op enum switches; it
    /// now resolves the switch's enum type and routes to the engine's enum overload, so the branch is tagged with the
    /// chosen state and carries a bare <c>resource_enum</c> operand (typedef + the state's inivalue). A value that is
    /// not one of the type's states builds no command.
    /// </summary>
    public class EnumCaseValueTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        // Builds an enum-keyed case on project2's "Custom blok" (its "NyTypeForThisProject" enum input) and returns the
        // committed project plus the case id — the switch the gateway must recognise as an enum.
        private static async Task<(Project Project, ElementId CaseId)> EnumKeyedCase()
        {
            Project original = await ReplayOracle.LoadProject("project2-CustomBlock.vis");
            ProjectEditor editor = original.Edit();
            FunctionBlockRef custom = editor.Group("Stue").FunctionBlock("Custom blok");
            SubProgramRef sub = custom.Program().AddSubProgram();
            CaseRef kase = sub.WhenTrue.AddCase("Case (%LT)", custom.Input("NyTypeForThisProject"));
            return (editor.ToProject(), kase.Id);
        }

        [Test]
        public async Task EnumCase_AddValue_TagsBranchWithStateAndEnumOperand()
        {
            (Project project, ElementId caseId) = await EnumKeyedCase();
            var session = new ProjectDocumentSession();
            session.Open(project);

            AddCaseValue? command = App.Commands.AddCaseValue(session.Current!, caseId, "Værdi2");
            Assert.That(command, Is.Not.Null, "the gateway now builds a command for an enum switch (no longer a no-op)");
            EditOutcome outcome = session.Apply(command!);

            ProjectElement kase = session.Current!.FindById(caseId)!;
            ProjectElement branch = kase.Children.First(c => c.Tag == "case_action" && c.GetAttribute("name") == "Værdi2");
            ProjectElement operand = branch.Children.First(c => c.Tag == "resource_enum");
            ProjectElement def = session.Current!.Root.Descendants().First(e => e.Tag == "enum_definition" && e.GetAttribute("name") == "NyTypeForThisProject");
            string expectedIniValue = def.Children.First(v => v.IsEnumValue && v.GetAttribute("name") == "Værdi2").Id!.Value.ToToken();
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(operand.GetAttribute("typedef"), Is.EqualTo(def.Id!.Value.ToToken()), "the operand references the switch's enum type");
                Assert.That(operand.GetAttribute("inivalue"), Is.EqualTo(expectedIniValue), "the operand's inivalue is the chosen state");
            });
        }

        [Test]
        public async Task EnumCase_AddValue_RejectsANonState()
        {
            (Project project, ElementId caseId) = await EnumKeyedCase();

            Assert.That(App.Commands.AddCaseValue(project, caseId, "NoSuchState"), Is.Null,
                "an entered value that is not one of the type's states builds no command");
        }
    }
}
