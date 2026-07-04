using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// Differential oracle against Microsoft's DTD validator: the serializer's output must validate against the
    /// inline DTD it emits in the same file. This breaks the read-wrong→write-wrong symmetry of the byte-identity
    /// suite (a misinterpretation both sides share reproduces the bytes and still passes those tests) and models
    /// the real rejection risk — IHC Visual is a validating consumer of exactly this grammar.
    /// </summary>
    public class DtdConformanceTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        internal static IReadOnlyList<string> ValidateAgainstOwnDtd(byte[] visBytes)
        {
            var events = new List<string>();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Parse,
                ValidationType = ValidationType.DTD,
                XmlResolver = null,
            };
            settings.ValidationEventHandler += (_, e) => events.Add(e.Message);
            using var reader = XmlReader.Create(new MemoryStream(visBytes), settings);
            while (reader.Read())
            {
            }
            return events;
        }

        [TestCase("Project0-Tomt.vis")]
        [TestCase("Project1-SimpelWired.vis")]
        [TestCase("project2-CustomBlock.vis")]
        [TestCase("project3-KompleksWired.vis")]
        [TestCase("project3-KompleksWired-mutated.vis")]
        [TestCase("LiveAuthored/step02-pir2.vis")]
        [TestCase("LiveAuthored/step06-luxtemp.vis")]
        public async Task SerializedOutput_ValidatesAgainstItsOwnInlineDtd(string oracle)
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load("testdata/" + oracle);

            byte[] bytes = ProjectSerializer.Serialize(project);
            IReadOnlyList<string> events = ValidateAgainstOwnDtd(bytes);

            Assert.That(events, Is.Empty,
                "an independent DTD-validating parse (what IHC Visual effectively does) found: "
                + string.Join(" | ", events));
        }
    }
}
