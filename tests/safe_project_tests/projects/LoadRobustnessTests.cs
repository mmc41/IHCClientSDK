using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Input that is not a loadable <c>.vis</c>/<c>.ihc</c> project must fail <see cref="ProjectAppService.Load(Stream)"/>
    /// fast with a typed <see cref="ProjectFormatException"/> carrying actionable context — never a raw
    /// XmlException, a silently truncated tree, a nonsense Project built from foreign XML, or dropped element text.
    /// </summary>
    public class LoadRobustnessTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> Load(byte[] bytes) =>
            new ProjectAppService(Settings).Load(new MemoryStream(bytes));

        private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

        private const string Doctype = """
            <!DOCTYPE utcs_project [
               <!ELEMENT utcs_project ANY>
               <!ATTLIST utcs_project version_major CDATA #REQUIRED
                              version_minor CDATA #REQUIRED
                              id1 CDATA #REQUIRED
                              id2 CDATA #REQUIRED
                              last_unique_id CDATA #REQUIRED>
               <!ELEMENT customer_info ANY>
               <!ATTLIST customer_info name CDATA "">
            ]>
            """;

        private static string Wrap(string body) =>
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" + Doctype + "\r\n" + body;

        [Test]
        public void Load_EmptyStream_ThrowsProjectFormatException() =>
            Assert.That(() => Load(System.Array.Empty<byte>()),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("empty"));

        [Test]
        public void Load_GzipData_ThrowsProjectFormatException_MentioningCompression() =>
            Assert.That(() => Load(new byte[] { 0x1F, 0x8B, 0x08, 0x00 }),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("gzip"));

        [Test]
        public void Load_Utf8Bom_ThrowsProjectFormatException()
        {
            byte[] body = Latin1(Wrap("<utcs_project version_major=\"4\" version_minor=\"0\" id1=\"_0x1\" id2=\"_0x2\" last_unique_id=\"_0x40\"/>"));
            byte[] withBom = new byte[body.Length + 3];
            withBom[0] = 0xEF;
            withBom[1] = 0xBB;
            withBom[2] = 0xBF;
            body.CopyTo(withBom, 3);

            Assert.That(() => Load(withBom),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("byte-order mark"));
        }

        [Test]
        public void Load_ForeignDeclaredEncoding_ThrowsProjectFormatException() =>
            Assert.That(() => Load(Latin1("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<utcs_project version_major=\"4\"/>")),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("UTF-8").And.Message.Contains("ISO-8859-1"));

        // review C3: XML permits whitespace around the '=' in an attribute; the foreign-encoding guard must still
        // fire for 'encoding = "UTF-8"'. The old declared-encoding regex required 'encoding=' with no surrounding
        // space, so a spaced declaration silently bypassed the guard and the file was transcoded instead of refused.
        [Test]
        public void Load_ForeignDeclaredEncoding_WithWhitespaceAroundEquals_ThrowsProjectFormatException() =>
            Assert.That(() => Load(Latin1("<?xml version=\"1.0\" encoding = \"UTF-8\"?>\r\n<utcs_project version_major=\"4\"/>")),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("UTF-8").And.Message.Contains("ISO-8859-1"));

        [Test]
        public void Load_WrongRootElement_ThrowsProjectFormatException() =>
            Assert.That(() => Load(Latin1("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<product_definition/>")),
                Throws.TypeOf<ProjectFormatException>()
                    .With.Message.Contains("product_definition").And.Message.Contains("utcs_project"));

        [Test]
        public void Load_MissingVersionMajor_ThrowsProjectFormatException() =>
            Assert.That(() => Load(Latin1("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project id1=\"_0x1\"/>")),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("version_major"));

        [Test]
        public void Load_TruncatedXml_ThrowsProjectFormatException_WithPosition() =>
            Assert.That(() => Load(Latin1("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project version_major=\"4\"><modified")),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("line"));

        [Test]
        public void Load_ElementTextContent_ThrowsProjectFormatException_NamingTheElementAndText()
        {
            byte[] bytes = Latin1(Wrap(
                "<utcs_project version_major=\"4\" version_minor=\"0\" id1=\"_0x1\" id2=\"_0x2\" last_unique_id=\"_0x40\">" +
                "<customer_info>Fritekst-note her</customer_info></utcs_project>"));

            Assert.That(() => Load(bytes),
                Throws.TypeOf<ProjectFormatException>()
                    .With.Message.Contains("customer_info").And.Message.Contains("Fritekst-note her"),
                "element text cannot be represented and must not be silently dropped");
        }

        [Test]
        public void Load_MalformedInlineDtdBlock_FailsAtLoad_WithContext()
        {
            // customer_info@name has no quoted default — under the old parser this crashed with a raw
            // ArgumentOutOfRangeException, and only when the schema view was first built (at save/validate).
            byte[] bytes = Latin1(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
                "<!DOCTYPE utcs_project [\r\n" +
                "   <!ELEMENT utcs_project ANY>\r\n" +
                "   <!ATTLIST utcs_project version_major CDATA #REQUIRED>\r\n" +
                "   <!ELEMENT customer_info ANY>\r\n" +
                "   <!ATTLIST customer_info name CDATA>\r\n" +
                "]>\r\n" +
                "<utcs_project version_major=\"4\"><customer_info/></utcs_project>");

            Assert.That(() => Load(bytes),
                Throws.TypeOf<ProjectFormatException>()
                    .With.Message.Contains("customer_info").And.Message.Contains("name"));
        }

        [Test]
        public void Load_DeeplyNestedDocument_ThrowsProjectFormatException()
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n");
            sb.Append("<utcs_project version_major=\"4\">");
            for (int i = 0; i < 200; i++)
            {
                sb.Append("<customer_info>");
            }
            for (int i = 0; i < 200; i++)
            {
                sb.Append("</customer_info>");
            }
            sb.Append("</utcs_project>");

            Assert.That(() => Load(Latin1(sb.ToString())),
                Throws.TypeOf<ProjectFormatException>().With.Message.Contains("nesting"));
        }

        [Test]
        public void Version_AbsentAttributes_ReturnsNull_NotAFabricatedDefault()
        {
            var bare = new Project(new ProjectElement("utcs_project", null,
                ImmutableArray<(string, string)>.Empty, ImmutableArray<ProjectElement>.Empty));

            Assert.That(bare.Version, Is.Null);
        }
    }
}
