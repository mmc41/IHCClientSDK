using System.Text;
using System.Threading.Tasks;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Small hardening guarantees: <see cref="Project.Modified"/> honors its null-on-malformed contract for
    /// out-of-range date parts; a vendor-like save without a <c>modified</c> element fails instead of breaking
    /// the id2/modified agreement; tabs in attribute values are escaped (a raw tab silently becomes a space on
    /// re-read) while other control characters fail; opt-in validate-before-save; <see cref="PackedStamp"/>
    /// range validation; and a subset-less DOCTYPE yields an empty (not garbage) DTD capture.
    /// </summary>
    public class MiscHardeningTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static Project MinimalProject(params (string, string)[] extraGroupAttrs)
        {
            var groupAttrs = new System.Collections.Generic.List<(string, string)> { ("name", "Stue") };
            groupAttrs.AddRange(extraGroupAttrs);
            return new Project(Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x40") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", groupAttrs.ToArray()))));
        }

        [Test]
        public void Modified_OutOfRangeDateParts_ReturnsNull()
        {
            var project = new Project(Node("utcs_project", null,
                new[] { ("version_major", "4") },
                Node("modified", null, new[] { ("year", "2026"), ("month", "13"), ("day", "1"), ("hour", "0"), ("minute", "0") })));

            Assert.That(project.Modified, Is.Null, "month=13 is malformed → null, never a throw");
        }

        [Test]
        public void DefaultSave_ProjectWithoutModifiedElement_Throws()
        {
            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();

            Assert.That(async () => await app.Save(MinimalProject(), ms, ProjectSaveOptions.Default),
                Throws.InvalidOperationException.With.Message.Contains("modified"),
                "stamping id2 without modified would silently break their minute-agreement invariant");
        }

        [Test]
        public async Task Serialize_TabInAttributeValue_IsEscaped_AndRoundTrips()
        {
            Project project = MinimalProject(("note", "A\tB"));

            byte[] bytes = ProjectSerializer.Serialize(project);
            string text = Encoding.Latin1.GetString(bytes);
            Project reloaded = await new ProjectAppService(Settings).Load(new MemoryStream(bytes));

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("A&#x9;B"), "a raw tab would be normalized to a space on re-read");
                Assert.That(reloaded.Groups[0].GetAttribute("note"), Is.EqualTo("A\tB"));
            });
        }

        [Test]
        public void Serialize_ControlCharacterInAttributeValue_Throws()
        {
            Project project = MinimalProject(("note", "A\u0001B"));

            Assert.That(() => ProjectSerializer.Serialize(project),
                Throws.InvalidOperationException.With.Message.Contains("U+0001"),
                "XML 1.0 cannot represent it — written raw, the file opens in no parser");
        }

        [Test]
        public void Serialize_AstralCharacter_ErrorNamesTheCombinedScalar_NotALoneSurrogate()
        {
            // Finding 18: an astral char is a surrogate PAIR; the non-Latin1 offender report must name the combined
            // scalar (U+1F600), not the lone high surrogate (U+D83D) the scan iterates first.
            Project project = MinimalProject(("note", System.Char.ConvertFromUtf32(0x1F600)));

            Assert.That(() => ProjectSerializer.Serialize(project),
                Throws.InstanceOf<InvalidOperationException>()
                    .With.Message.Contains("U+1F600").And.Message.Not.Contains("U+D83D"),
                "the diagnostic names the real code point, not a surrogate half");
        }

        [Test]
        public void Save_WithValidateBeforeSave_RejectsAnInvalidProject()
        {
            // A dangling scenes binding: serializable, but invalid per the checklist.
            var project = new Project(Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x60") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Stue") },
                        Node("product_dataline", "_0x5153", new[] { ("product_identifier", "_0x2202"), ("name", "P") },
                            Node("scenes", "_0x5349", new[] { ("name", "S"), ("scene_resource", "_0xdead52") }))))));
            var app = new ProjectAppService(Settings);
            using var ms = new MemoryStream();
            var options = new ProjectSaveOptions { WriteMetadataVerbatim = true, ValidateBeforeSave = true };

            Assert.That(async () => await app.Save(project, ms, options),
                Throws.TypeOf<ProjectValidationException>());
        }

        [Test]
        public void PackedStamp_OutOfRangeComponents_Throw()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => new PackedStamp(32, 0, 0, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => new PackedStamp(1, 24, 0, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => new PackedStamp(1, 0, 60, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(() => new PackedStamp(1, 0, 0, 60), Throws.TypeOf<System.ArgumentOutOfRangeException>());
                Assert.That(new PackedStamp(4, 16, 5, 51).ToToken(), Is.EqualTo("_0x4100533"));
            });
        }

        [Test]
        public void InlineDtd_QuotedSubsetClose_IsNotTruncated()
        {
            // A quoted "]>" inside an ATTLIST default must not be mistaken for the internal-subset terminator.
            byte[] bytes = Encoding.Latin1.GetBytes(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
                "<!DOCTYPE utcs_project [\r\n" +
                "   <!ELEMENT utcs_project ANY>\r\n" +
                "   <!ELEMENT widget ANY>\r\n" +
                "   <!ATTLIST widget note CDATA \"a]>b\">\r\n" +
                "]>\r\n" +
                "<utcs_project/>");

            var blocks = InlineDtd.Capture(bytes);

            Assert.Multiple(() =>
            {
                Assert.That(blocks.ContainsKey("widget"), Is.True, "the block after the quoted ]> is captured, not truncated away");
                Assert.That(ProjectSchemaRegistry.ParseBlock(blocks["widget"]).FindAttr("note")!.Default, Is.EqualTo("a]>b"),
                    "the quoted ]> is part of the default, not the subset terminator");
            });
        }

        [Test]
        public void InlineDtd_SubsetlessDoctype_YieldsEmptyCapture()
        {
            byte[] bytes = Encoding.Latin1.GetBytes(
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
                "<!DOCTYPE utcs_project SYSTEM \"utcs.dtd\">\r\n" +
                "<utcs_project version_major=\"4\" note=\"[ not a dtd ]>\"/>");

            Assert.That(InlineDtd.Capture(bytes), Is.Empty,
                "no internal subset → nothing captured, and the body is never scanned for stray brackets");
        }
    }
}
