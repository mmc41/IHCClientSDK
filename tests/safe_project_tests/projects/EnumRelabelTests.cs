using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T013 / US-030 / PG-5 — relabeling an EXISTING value of a USER enum type: <see cref="ProjectEditor.RelabelEnumValue"/>
    /// changes the value's label in place, preserving its id and <c>index</c> so only the label byte-differs and the
    /// change round-trips faithfully, and refuses a built-in catalog type ("[read only]"). Reorder / remove /
    /// rename-type stay out of scope (D05).
    /// </summary>
    public class EnumRelabelTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static Project Load(string file) => ProjectReader.Read(TestData.ReadBytes("projects/" + file));

        private static ProjectElement EnumType(Project p, string name) =>
            p.Root.Descendants().First(e => e.Tag == "enum_definition" && e.GetAttribute("name") == name);

        // "PIR funktion" is a user type (no typeid); its first value has the index-0 attribute elided — a relabel must
        // preserve that elision (no reorder) as well as the value's id.
        [Test]
        public async Task EnumRelabel_UserTypeState_ChangesLabelPreservingIdAndElidedIndex()
        {
            Project project = Load("Project1-SimpelWired.vis");
            ElementId valueId = EnumType(project, "PIR funktion").ChildrenOrEmpty().First(v => v.Tag == "enum_value").Id!.Value;

            ProjectEditor editor = project.Edit();
            editor.RelabelEnumValue(editor.EnumDefinition("PIR funktion"), valueId, "Relabeled");
            Project after = editor.ToProject();

            using var ms = new MemoryStream();
            await App.Save(after, ms);
            Project reloaded = ProjectReader.Read(ms.ToArray());

            ProjectElement value = reloaded.FindById(valueId)!;
            Assert.Multiple(() =>
            {
                Assert.That(value.GetAttribute("name"), Is.EqualTo("Relabeled"), "the label changed");
                Assert.That(value.Id, Is.EqualTo(valueId), "the value id is preserved");
                Assert.That(value.GetAttribute("index"), Is.Null, "the index-0 elision is preserved (no reorder)");
            });
        }

        // A built-in ("Persienne tilstand", typeid _0x10) is "[read only]" — relabeling its values is refused.
        [Test]
        public void EnumRelabel_BuiltInType_IsRefused()
        {
            Project project = Load("Project1-SimpelWired.vis");
            ElementId valueId = EnumType(project, "Persienne tilstand").ChildrenOrEmpty().First(v => v.Tag == "enum_value").Id!.Value;
            ProjectEditor editor = project.Edit();

            InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() =>
                editor.RelabelEnumValue(editor.EnumDefinition("Persienne tilstand"), valueId, "Hacked"));
            Assert.That(ex!.Message, Does.Contain("read only"), "the refusal names the read-only cause");
        }
    }
}
