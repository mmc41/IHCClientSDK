using System.IO;
using System.Text;
using Ihc.Projects;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Deterministic (no install dir) tests for <see cref="CatalogReader"/>: it must materialize the file's own
    /// internal-DTD ATTLIST defaults (unlike the .vis reader) and honor a byte-order mark over a contradicting
    /// declared encoding — the two behaviors the catalog/insert path depends on (spec ch. 09 §9.3.2/§9.3.7).
    /// </summary>
    public class CatalogReaderTests
    {
        [Test]
        public void Read_MaterializesDtdDefaults_ForOmittedAttributes()
        {
            // A .def-style fragment: the instance omits locked/backup; the DTD supplies their defaults.
            const string xml =
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

            using var stream = new MemoryStream(Encoding.Latin1.GetBytes(xml));
            ProjectElement root = CatalogReader.Read(stream);

            Assert.Multiple(() =>
            {
                Assert.That(root.GetAttribute("locked"), Is.EqualTo("yes"), "DTD default for omitted locked");
                ProjectElement? output = root.FindChild("dataline_output");
                Assert.That(output, Is.Not.Null);
                Assert.That(output!.GetAttribute("backup"), Is.EqualTo("yes"), "DTD default for omitted backup");
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
    }
}
