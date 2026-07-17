using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsCheck;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Property-based round-trip laws for authored attribute text, using CsCheck.
    ///
    /// The rest of this suite pins fidelity against a fixed set of oracle files, which is exactly right for the
    /// wire format but only ever exercises the character sequences those oracles happen to contain. Attribute text
    /// is user-authored (locality names, notes), so the interesting inputs are the ones no oracle has: XML
    /// metacharacters, quotes, Danish letters, and whitespace at the edges. This generalizes the law the oracles
    /// imply — <c>Load(Save(x))</c> preserves what was written, exactly — over that alphabet.
    ///
    /// Motivated by precedent: CsCheck previously found a real UTF-8 round-trip defect in the SOAP serializer, and
    /// this engine has its own encoding subtleties (a Latin-1 declared document).
    /// </summary>
    public class AttributeRoundTripPropertyTests
    {
        private const string Oracle = "testdata/projects/Project1-SimpelWired.vis";

        private static IhcSettings Settings => TestSetup.Settings;

        /// <summary>
        /// The characters an escaping or encoding bug actually hides behind: XML metacharacters, both quote forms,
        /// the Danish letters this vendor's projects are full of, a Latin-1 edge, and spaces.
        /// </summary>
        private const string Alphabet = "ab z09&<>\"'æøåÆØÅ§é%_-./:";

        private static readonly Gen<string> AttributeText =
            Gen.OneOfConst(Alphabet.ToCharArray()).Array[1, 24].Select(cs => new string(cs));

        /// <summary>Writes <paramref name="name"/> onto the first locality, saves, reloads, and reads it back.</summary>
        private static async Task<string?> RoundTripLocalityName(string name)
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load(Oracle);

            ElementId localityId = project.Groups.First().Id!.Value;
            ProjectEditor editor = project.Edit();
            editor.TryResolve(localityId, out ElementRef? locality);
            locality!.SetAttribute("name", name);

            using var buffer = new MemoryStream();
            await app.Save(editor.ToProject(), buffer);

            buffer.Position = 0;
            Project reloaded = await app.Load(buffer);
            return reloaded.FindById(localityId)!.GetAttribute("name");
        }

        /// <summary>
        /// The law: whatever text is authored onto an attribute is the text that comes back. Escaping is the
        /// serializer's business and must be invisible to the caller.
        /// </summary>
        [Test]
        public void SaveThenLoad_PreservesAuthoredAttributeText_Exactly()
        {
            AttributeText.Sample(name =>
            {
                string? reloaded = RoundTripLocalityName(name).GetAwaiter().GetResult();
                return reloaded == name;
            }, iter: 200);
        }

        /// <summary>
        /// Negative control for the two property tests above: proves the round trip really is carrying text through
        /// XML escaping, rather than the laws holding because nothing needing escaping ever reaches the writer.
        /// </summary>
        [Test]
        public async Task Save_ActuallyEscapes_XmlMetacharactersOnTheWire()
        {
            var app = new ProjectAppService(Settings);
            Project project = await app.Load(Oracle);
            ElementId localityId = project.Groups.First().Id!.Value;
            ProjectEditor editor = project.Edit();
            editor.TryResolve(localityId, out ElementRef? locality);
            locality!.SetAttribute("name", "Stue & Køkken \"åben\" <1>");

            using var buffer = new MemoryStream();
            await app.Save(editor.ToProject(), buffer);
            string wire = new System.Text.UTF8Encoding(false).GetString(buffer.ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(wire, Does.Contain("&amp;"), "'&' must be escaped on the wire");
                Assert.That(wire, Does.Not.Contain("Stue & K"), "the raw ampersand must not appear unescaped");
            });
        }

        /// <summary>
        /// The same law one turn further: a second save/load must not drift either. An escaping bug that
        /// double-escapes survives one round trip and only shows up on the next.
        /// </summary>
        [Test]
        public void SaveThenLoad_IsIdempotent_AcrossTwoRoundTrips()
        {
            AttributeText.Sample(name =>
            {
                string? once = RoundTripLocalityName(name).GetAwaiter().GetResult();
                string? twice = RoundTripLocalityName(once!).GetAwaiter().GetResult();
                return once == twice;
            }, iter: 100);
        }
    }
}
