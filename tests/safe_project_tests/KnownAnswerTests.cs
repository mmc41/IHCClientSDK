using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Differential known-answer oracle: the SDK model's values for an authentic file must agree with an
    /// independent parser (<see cref="XmlDocument"/>) reading the same bytes. The byte-identity suite proves
    /// Read∘Serialize is the identity on bytes; only an independent read proves the model holds the RIGHT
    /// values — a reader bug mirrored by the writer reproduces the bytes with a wrong model.
    /// </summary>
    public class KnownAnswerTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private static XmlDocument LoadRaw(byte[] bytes)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using var reader = XmlReader.Create(new MemoryStream(bytes), settings);
            var document = new XmlDocument();
            document.Load(reader);
            return document;
        }

        [Test]
        public async Task Project1_ModelValues_AgreeWithAnIndependentParser()
        {
            byte[] bytes = TestData.ReadBytes("Project1-SimpelWired.vis");
            Project project = await new ProjectAppService(Settings).Load(new MemoryStream(bytes));
            XmlDocument raw = LoadRaw(bytes);
            XmlElement root = raw.DocumentElement!;

            List<string> rawGroupNames = root.SelectNodes("groups/group")!.Cast<XmlElement>()
                .Select(g => g.GetAttribute("name")).ToList();
            XmlElement rawModified = (XmlElement)root.SelectSingleNode("modified")!;
            XmlElement rawFirstProduct = (XmlElement)root.SelectSingleNode("groups/group/product_dataline")!;

            Assert.Multiple(() =>
            {
                Assert.That(project.Id1, Is.EqualTo(root.GetAttribute("id1")));
                Assert.That(project.LastUniqueId, Is.EqualTo(root.GetAttribute("last_unique_id")));
                Assert.That(project.Groups.Select(g => g.GetAttribute("name")), Is.EqualTo(rawGroupNames));
                Assert.That(project.Modified!.Value.Year.ToString(), Is.EqualTo(rawModified.GetAttribute("year")));
                Assert.That(project.Modified!.Value.Minute.ToString(), Is.EqualTo(rawModified.GetAttribute("minute")));
                Assert.That(ElementId.TryParse(rawFirstProduct.GetAttribute("id"), out ElementId productId), Is.True);
                Assert.That(project.FindById(productId)!.GetAttribute("name"),
                    Is.EqualTo(rawFirstProduct.GetAttribute("name")),
                    "id-addressed resolution returns the element the independent parser sees");
            });
        }
    }
}
