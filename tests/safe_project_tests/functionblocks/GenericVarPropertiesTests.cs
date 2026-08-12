using System.Linq;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T015 / US-027 / US-026 / PG-9 — an ordinary FB resource variable edits its Name and Note through the generic
    /// name/note command (<see cref="RenameLocality"/> — the same command localities and function blocks use), which
    /// the T003 central predicate refuses inside a locked block.
    /// </summary>
    public class GenericVarPropertiesTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static Task<Project> Load() => App.Load("testdata/projects/project2-CustomBlock.vis");

        private static ElementId OutputOf(Project p, string fbName) =>
            p.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == fbName)
                .FindChild("outputs")!.ChildrenOrEmpty().First(e => e.Tag == "resource_output").Id!.Value;

        [Test]
        public async Task GenericVarProperties_RenamesAnUnlockedVariableNameAndNote()
        {
            Project project = await Load();
            ElementId varId = OutputOf(project, "Custom blok");   // a variable of an UNLOCKED function block
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(App.Commands.RenameLocality(session.Current!, varId, "Renamed", "a note"));

            ProjectElement variable = session.Current!.FindById(varId)!;
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(variable.GetAttribute("name"), Is.EqualTo("Renamed"), "the variable name is edited");
                Assert.That(variable.GetAttribute("note"), Is.EqualTo("a note"), "the variable note is edited");
            });
        }

        [Test]
        public async Task GenericVarProperties_OnVariableInsideLockedBlock_IsRefused()
        {
            Project project = await Load();
            ElementId varId = OutputOf(project, "AutoProof");   // a variable inside a LOCKED library block
            var session = new ProjectDocumentSession();
            session.Open(project);

            EditOutcome outcome = session.Apply(App.Commands.RenameLocality(session.Current!, varId, "Hacked", "x"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(outcome.Reason, Does.Contain("låst funktionsblok"));
            });
        }
    }
}
