using System.Linq;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T011 / US-030 / PG-4 — the enum-type picker on enum-variable insert: choosing an EXISTING enumerator type
    /// (a built-in or any project-global type) inserts a <c>resource_enum</c> that references that type's def-id and
    /// authors NO new type. <see cref="ProjectProjections"/>'s <c>GetEnumeratorTypes()</c> is the picker list.
    /// </summary>
    public class EnumPickerTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static int EnumDefCount(Project p) => p.Root.Descendants().Count(e => e.Tag == "enum_definition");

        [Test]
        public void EnumPicker_ListsTheBuiltInTypes_ExcludingUserTexts()
        {
            Project project = App.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));

            Assert.That(project.GetEnumeratorTypes(), Is.EqualTo(new[] { "Persienne tilstand", "Logning" }),
                "a fresh project offers exactly its two built-in enumerators");
        }

        [Test]
        public void EnumPicker_PickExistingType_AddsVariableReferencingItsDefId_NoNewType()
        {
            var svc = App;
            Project project = svc.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId localityId = project.Groups[0].Id!.Value;
            ElementId fbId = session.Apply(svc.Commands.AddEmptyFunctionBlock(session.Current!, localityId, "FB")).Value;
            ElementId settingsId = session.Current!.FindById(fbId)!.FindChild("settings")!.Id!.Value;
            int defsBefore = EnumDefCount(session.Current!);

            AddEnumVariableOfExistingType? command = svc.Commands.AddEnumVariableOfType(session.Current!, settingsId, "MyEnum", "Persienne tilstand");
            Assert.That(command, Is.Not.Null);
            EditOutcome<ElementId> outcome = session.Apply(command!);

            ProjectElement variable = session.Current!.FindById(outcome.Value)!;
            ProjectElement builtIn = session.Current!.Root.Descendants()
                .First(e => e.Tag == "enum_definition" && e.GetAttribute("name") == "Persienne tilstand");
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(EnumDefCount(session.Current!), Is.EqualTo(defsBefore), "picking an existing type authors NO new enum type");
                Assert.That(variable.Tag, Is.EqualTo("resource_enum"));
                Assert.That(variable.GetAttribute("typedef"), Is.EqualTo(builtIn.Id!.Value.ToToken()), "references the existing type's def-id");
                Assert.That(variable.GetAttribute("inivalue"), Is.EqualTo(builtIn.ChildrenOrEmpty().First(v => v.Tag == "enum_value").Id!.Value.ToToken()),
                    "the initial value is the type's first state");
            });
        }

        // T012 / US-030 / PG-7 / D02: the standalone route authors a 0-state, unreferenced, project-global type — no variable.
        [Test]
        public void StandaloneEnum_AuthorsAZeroStateUnreferencedProjectGlobalType()
        {
            var svc = App;
            Project project = svc.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            var session = new ProjectDocumentSession();
            session.Open(project);
            int defsBefore = EnumDefCount(session.Current!);

            session.Apply(svc.Commands.AddStandaloneEnumType(session.Current!, "MyStandalone", System.Array.Empty<string>()));

            ProjectElement type = session.Current!.Root.Descendants().First(e => e.Tag == "enum_definition" && e.GetAttribute("name") == "MyStandalone");
            Assert.Multiple(() =>
            {
                Assert.That(EnumDefCount(session.Current!), Is.EqualTo(defsBefore + 1), "one new type authored");
                Assert.That(type.ChildrenOrEmpty().Count(c => c.Tag == "enum_value"), Is.EqualTo(0), "a 0-state type");
                Assert.That(session.Current!.FindParent(type.Id!.Value)!.Tag, Is.EqualTo("enum_definitions"), "project-global");
                Assert.That(session.Current!.Root.Descendants().Any(e => e.Tag == "resource_enum" && e.GetAttribute("typedef") == type.Id!.Value.ToToken()),
                    Is.False, "unreferenced — no variable was inserted");
            });
        }

        [Test]
        public async Task StandaloneEnum_EmptyType_ByteRoundTrips()
        {
            var svc = App;
            Project project = svc.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            var session = new ProjectDocumentSession();
            session.Open(project);
            session.Apply(svc.Commands.AddStandaloneEnumType(session.Current!, "MyStandalone", System.Array.Empty<string>()));

            using var ms = new MemoryStream();
            await svc.Save(session.Current!, ms);
            Project reloaded = ProjectReader.Read(ms.ToArray());

            ProjectElement type = reloaded.Root.Descendants().First(e => e.Tag == "enum_definition" && e.GetAttribute("name") == "MyStandalone");
            Assert.That(type.ChildrenOrEmpty().Count(c => c.Tag == "enum_value"), Is.EqualTo(0), "the empty standalone type survives save→reload");
        }
    }
}
