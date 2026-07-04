using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The inline-DTD block parser must produce defaults exactly as a conforming XML reader computes logical
    /// attribute values — whitespace inside quoted defaults preserved (§3.3.3), entities decoded, single quotes
    /// and multiple/non-adjacent ATTLIST declarations accepted — because omit-if-default compares those defaults
    /// against the reader's logical values: a mangled default silently deletes an attribute on save.
    /// </summary>
    public class SchemaRegistryHardeningTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        [Test]
        public void ParseBlock_DoubleSpaceInsideQuotedDefault_IsPreserved()
        {
            ElementSchema schema = ProjectSchemaRegistry.ParseBlock(
                "<!ELEMENT fancy ANY>\r\n<!ATTLIST fancy id ID #REQUIRED\r\n               name CDATA \"My  Component\">\r\n");

            Assert.That(schema.Attrs.Single(a => a.Name == "name").Default, Is.EqualTo("My  Component"),
                "whitespace inside a quoted default maps one-to-one, never run-collapsed");
        }

        [Test]
        public void ParseBlock_MultipleAttlistsForOneElement_MergeTheirAttributes()
        {
            ElementSchema schema = ProjectSchemaRegistry.ParseBlock(
                "<!ELEMENT fancy ANY>\r\n<!ATTLIST fancy id ID #REQUIRED>\r\n<!ATTLIST fancy name CDATA \"\">\r\n");

            Assert.That(schema.Attrs.Select(a => a.Name), Is.EqualTo(new[] { "id", "name" }));
        }

        [Test]
        public void ParseBlock_SingleQuotedDefault_Parses()
        {
            ElementSchema schema = ProjectSchemaRegistry.ParseBlock(
                "<!ELEMENT fancy ANY>\r\n<!ATTLIST fancy state CDATA 'on'>\r\n");

            Assert.That(schema.Attrs.Single().Default, Is.EqualTo("on"));
        }

        [Test]
        public void ParseBlock_EntityReferencesInDefault_AreDecodedToLogicalValues()
        {
            ElementSchema schema = ProjectSchemaRegistry.ParseBlock(
                "<!ELEMENT fancy ANY>\r\n<!ATTLIST fancy note CDATA \"A &amp; B &#x41;\">\r\n");

            Assert.That(schema.Attrs.Single().Default, Is.EqualTo("A & B A"),
                "the default must be comparable with the reader's unescaped logical values");
        }

        [Test]
        public void ParseBlock_MissingDefault_ThrowsVisSchemaFormat_NamingTagAndAttribute() =>
            Assert.That(() => ProjectSchemaRegistry.ParseBlock(
                    "<!ELEMENT fancy ANY>\r\n<!ATTLIST fancy name CDATA>\r\n"),
                Throws.TypeOf<VisSchemaFormatException>()
                    .With.Message.Contains("fancy").And.Message.Contains("name"));

        [Test]
        public void ParseBlock_UnterminatedEnumeration_ThrowsVisSchemaFormat() =>
            Assert.That(() => ProjectSchemaRegistry.ParseBlock(
                    "<!ELEMENT fancy ANY>\r\n<!ATTLIST fancy locked (yes | no\r\n>"),
                Throws.TypeOf<VisSchemaFormatException>().With.Message.Contains("fancy"));

        [Test]
        public async Task RoundTrip_BodyValueDifferingFromDoubleSpacedDefault_KeepsTheAttribute()
        {
            // The F24 repro: the DTD default carries two spaces, the body value one. A run-collapsing parser
            // equated them and omit-if-default silently deleted the attribute on an edit-free load→save.
            byte[] original = Encoding.Latin1.GetBytes(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
                "<!DOCTYPE utcs_project [\r\n" +
                "   <!ELEMENT utcs_project ANY>\r\n" +
                "   <!ATTLIST utcs_project version_major CDATA #REQUIRED\r\n" +
                "                  version_minor CDATA #REQUIRED\r\n" +
                "                  id1 CDATA #REQUIRED\r\n" +
                "                  id2 CDATA #REQUIRED\r\n" +
                "                  last_unique_id CDATA #REQUIRED>\r\n" +
                "   <!ELEMENT customer_info ANY>\r\n" +
                "   <!ATTLIST customer_info name CDATA \"My  Component\">\r\n" +
                "]>\r\n" +
                "<utcs_project version_major=\"4\" version_minor=\"0\" id1=\"_0x1\" id2=\"_0x2\" last_unique_id=\"_0x40\">\r\n" +
                "   <customer_info name=\"My Component\"/>\r\n" +
                "</utcs_project>\r\n");

            var app = new ProjectAppService(Settings);
            Project project = await app.Load(new MemoryStream(original));
            using var ms = new MemoryStream();
            await app.Save(project, ms, ProjectSaveOptions.PreserveExistingMetadata);
            string saved = Encoding.Latin1.GetString(ms.ToArray());

            Assert.That(saved, Does.Contain("name=\"My Component\""),
                "the value differs from the (two-space) default, so it must be written explicitly");
        }
    }
}
