using System.Collections.Immutable;
using System.IO;
using System.Text;

using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Deterministic (no install dir) tests for <see cref="CatalogReader"/>: it captures the <b>raw</b> file body
    /// (only the attributes physically present, source order, file id tokens) so <see cref="CatalogFileWriter"/>
    /// re-emits it faithfully, and honors a byte-order mark over a contradicting declared encoding. The file's own
    /// internal-DTD ATTLIST defaults are no longer materialized on read — the insert path re-derives them on demand via
    /// <see cref="CatalogDefaults"/> (spec ch. 09 §9.3.2/§9.3.7).
    /// </summary>
    public class CatalogReaderTests
    {
        // A .def-style fragment: the instance omits locked/backup; the DTD supplies their defaults.
        private const string OmittedDefaultsXml =
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
            "<!DOCTYPE product_dataline[\r\n" +
            "   <!ELEMENT product_dataline ANY>\r\n" +
            "   <!ATTLIST product_dataline id ID #REQUIRED\r\n" +
            "                  locked (yes | no) \"yes\"\r\n" +
            "                  name CDATA \"\">\r\n" +
            "   <!ELEMENT dataline_output ANY>\r\n" +
            "   <!ATTLIST dataline_output id ID #REQUIRED\r\n" +
            "                  backup (yes | no) \"yes\">\r\n" +
            "]>\r\n" +
            "<product_dataline id=\"_0x01\" name=\"X\">\r\n" +
            "  <dataline_output id=\"_0x02\"/>\r\n" +
            "</product_dataline>";

        [Test]
        public void Read_CapturesRawAttributes_WithoutMaterializingDtdDefaults()
        {
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(OmittedDefaultsXml));
            ProjectElement root = CatalogReader.Read(stream);

            Assert.Multiple(() =>
            {
                // Raw body: attributes the file omits are NOT materialized (that now happens on insert).
                Assert.That(root.GetAttribute("locked"), Is.Null, "omitted locked stays absent in the raw body");
                Assert.That(root.GetAttribute("name"), Is.EqualTo("X"), "present attributes are captured");
                ProjectElement? output = root.FindChild("dataline_output");
                Assert.That(output, Is.Not.Null);
                Assert.That(output!.GetAttribute("backup"), Is.Null, "omitted backup stays absent in the raw body");
            });
        }

        [Test]
        public void CatalogDefaults_Materialize_AppliesTheDtdDefaultsTheReaderNoLongerDoes()
        {
            byte[] bytes = Encoding.Latin1.GetBytes(OmittedDefaultsXml);
            ProjectElement raw = CatalogReader.Read(bytes: bytes);
            ProjectSchemaView grammar = ProjectSchemaView.For(InlineDtd.Capture(bytes));

            ProjectElement effective = CatalogDefaults.Materialize(raw, grammar);

            Assert.Multiple(() =>
            {
                Assert.That(effective.GetAttribute("locked"), Is.EqualTo("yes"), "DTD default for omitted locked, materialized on demand");
                Assert.That(effective.FindChild("dataline_output")!.GetAttribute("backup"), Is.EqualTo("yes"), "DTD default for omitted backup");
            });
        }

        [Test]
        public void Read_HonorsUtf8Bom_OverDeclaredIso88591()
        {
            // The Products\*.def trap: UTF-8 BOM + body bytes, but the declaration claims ISO-8859-1.
            const string xml =
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
                "<product_dataline id=\"_0x01\" name=\"højre\"/>";
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] body = Encoding.UTF8.GetBytes(xml);
            byte[] bytes = new byte[bom.Length + body.Length];
            bom.CopyTo(bytes, 0);
            body.CopyTo(bytes, bom.Length);

            using var stream = new MemoryStream(bytes);
            ProjectElement root = CatalogReader.Read(stream);

            Assert.That(root.GetAttribute("name"), Is.EqualTo("højre"));
        }

        // A .def with an inline DTD: instance omits locked (rides the DTD default), display name carries the vendor
        // NN# menu prefix, and the DTD blocks must be captured for open-world insert.
        private const string ProductDefXml =
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
            "<!DOCTYPE product_dataline[\r\n" +
            "   <!ELEMENT product_dataline ANY>\r\n" +
            "   <!ATTLIST product_dataline id ID #REQUIRED\r\n" +
            "                  product_identifier CDATA #REQUIRED\r\n" +
            "                  locked (yes | no) \"yes\"\r\n" +
            "                  name CDATA \"\">\r\n" +
            "   <!ELEMENT dataline_output ANY>\r\n" +
            "   <!ATTLIST dataline_output id ID #REQUIRED>\r\n" +
            "]>\r\n" +
            "<product_dataline id=\"_0x01\" product_identifier=\"_0x2101\" name=\"12#Stikkontakt\">\r\n" +
            "  <dataline_output id=\"_0x02\"/>\r\n" +
            "</product_dataline>";

        [Test]
        public void ReadProduct_MapsIdentityDtdDefaultsAndInlineDtd()
        {
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(ProductDefXml));
            ProductDefinition product = CatalogReader.ReadProduct(stream);

            Assert.Multiple(() =>
            {
                Assert.That(product.ProductIdentifier, Is.EqualTo("_0x2101"), "product_identifier");
                Assert.That(product.DisplayName, Is.EqualTo("Stikkontakt"), "NN# menu prefix stripped");
                Assert.That(product.CategoryPath, Is.Empty, "standalone file has no catalog-tree category");
                Assert.That(product.Body.GetAttribute("locked"), Is.Null, "raw body: omitted locked is not materialized on read");
                Assert.That(product.Grammar.TryGetDeclaration("product_dataline"), Is.Not.Null,
                    "inline DTD parsed into the structured grammar for open-world insert");
                Assert.That(product.Grammar.DoctypeRoot, Is.EqualTo("product_dataline"), "DOCTYPE root is grammar data");
                Assert.That(product.Grammar.TryGetDeclaration("product_dataline")!.FindAttr("locked")!.RawLiteral,
                    Is.EqualTo("yes"), "the catalog default rides the grammar for faithful re-emission");
                Assert.That(product.Documentation.IsEmpty, Is.True, "no documentation supplied → Empty");
            });
        }

        [Test]
        public void ReadProduct_AttachesSuppliedDocumentation()
        {
            var documentation = new DefinitionDocumentation("Overview help text", ImmutableDictionary<string, string>.Empty);
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(ProductDefXml));

            ProductDefinition product = CatalogReader.ReadProduct(stream, documentation);

            Assert.That(product.Documentation.Summary, Is.EqualTo("Overview help text"));
        }

        [Test]
        public void ReadFunctionBlock_MapsMasterFieldsAndInlineDtd()
        {
            // .ifb files are genuine ISO-8859-1; the block name carries Danish letters that must decode correctly.
            const string xml =
                "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n" +
                "<!DOCTYPE functionblock[\r\n" +
                "   <!ELEMENT functionblock ANY>\r\n" +
                "   <!ATTLIST functionblock id ID #REQUIRED\r\n" +
                "                  master_type CDATA #IMPLIED\r\n" +
                "                  master_version CDATA #IMPLIED\r\n" +
                "                  master_name CDATA #IMPLIED\r\n" +
                "                  name CDATA #IMPLIED>\r\n" +
                "]>\r\n" +
                "<functionblock id=\"_0x01\" master_type=\"1.1.01\" master_version=\"e\" " +
                "master_name=\"Kip tænd sluk\" name=\"1.1.01.e. Kip tænd sluk\"/>";

            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(xml));
            FunctionBlockDefinition block = CatalogReader.ReadFunctionBlock(stream);

            Assert.Multiple(() =>
            {
                Assert.That(block.MasterType, Is.EqualTo("1.1.01"), "master_type");
                Assert.That(block.MasterVersion, Is.EqualTo("e"), "master_version");
                Assert.That(block.MasterName, Is.EqualTo("Kip tænd sluk"), "master_name (Latin-1 decoded)");
                Assert.That(block.DisplayName, Is.EqualTo("1.1.01.e. Kip tænd sluk"), "name attribute verbatim, not menu-stripped");
                Assert.That(block.CategoryPath, Is.Empty);
                Assert.That(block.Grammar.TryGetDeclaration("functionblock"), Is.Not.Null,
                    "inline DTD parsed into the structured grammar");
                Assert.That(block.IsEmptyTemplate, Is.False, "a real block is not the empty template scaffold");
            });
        }
    }
}
