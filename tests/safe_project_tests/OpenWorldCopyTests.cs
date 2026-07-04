using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Copy/insert of an element type the static registry does not declare (open-world: grammar from the file's
    /// own inline DTD) must still allocate fresh ids — keeping the source id verbatim mints a literal duplicate id
    /// in the document, which IHC Visual resolves ambiguously. The type code for the fresh id is derived from the
    /// source id token when the tag is unregistered; an unparseable id on such a tag is an error, not a passthrough.
    /// </summary>
    public class OpenWorldCopyTests
    {
        private const string Fixture = "Synthetic/OpenWorldCustomComponent.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadFixture() =>
            new ProjectAppService(Settings).Load("testdata/" + Fixture);

        [Test]
        public async Task CopySubtree_UnregisteredElementType_AllocatesAFreshId()
        {
            Project project = await LoadFixture();
            var app = new ProjectAppService(Settings);
            ProjectEditor editor = project.Edit();

            ProjectElement widget = project.Root.Descendants().Single(e => e.Tag == "custom_widget");
            ProjectElement target = project.Root.Descendants().Single(e => e.Tag == "dataline_input_modules");

            ElementId copyId = editor.CopySubtree(widget.Id!.Value, target.Id!.Value);
            Project after = editor.ToProject();

            var widgets = after.Root.Descendants().Where(e => e.Tag == "custom_widget").ToList();
            Assert.Multiple(() =>
            {
                Assert.That(widgets, Has.Count.EqualTo(2), "source and copy both exist");
                Assert.That(copyId, Is.Not.EqualTo(widget.Id!.Value), "the copy has a fresh id");
                Assert.That(copyId.TypeCode, Is.EqualTo(widget.Id!.Value.TypeCode),
                    "the fresh id keeps the source token's type-code suffix");
                Assert.That(widgets.Select(w => w.GetAttribute("id")).Distinct().Count(), Is.EqualTo(2),
                    "no duplicate id token in the document");
                ProjectValidationResult v = app.Validate(after);
                Assert.That(v.IsValid, Is.True, "no duplicate counters: " + string.Join(" | ", v.Errors));
            });
        }

        [Test]
        public async Task InsertTransform_UnregisteredTagWithUnparseableId_Throws()
        {
            Project project = await LoadFixture();
            var body = new ProjectElement("custom_widget", null,
                ImmutableArray.Create(("id", "not-a-token")), ImmutableArray<ProjectElement>.Empty);
            ProjectElement enumDefinitions = project.Child("enum_definitions")!;

            Assert.That(
                () => InsertTransform.Insert(body, new IdAllocator(0x100), enumDefinitions,
                                             ProjectSchemaView.For(project.InlineDtdBlocks)),
                Throws.InvalidOperationException.With.Message.Contains("custom_widget"));
        }
    }
}
