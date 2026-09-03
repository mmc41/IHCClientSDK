using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// W5 / F9 (uxparity2 T020): a variable carries TWO documentation fields, not one. The reference application's
    /// properties dialog shows both — control id 214 <i>Tekst til funktionsdokumentation</i> and id 517
    /// <i>Noter for hjælpetekst</i> (measured live) — and the `.vis` grammar has
    /// always declared the second as <c>note-2</c>. What was missing is an SDK read/write surface for it.
    /// <para>
    /// The risk this task carries is byte fidelity, so these tests are round-trip tests: <c>note-2</c> defaults to the
    /// empty string in the DTD, and an attribute at its default is ELIDED on write. Adding a help-note surface must
    /// therefore leave a project that never set one byte-identical.
    /// </para>
    /// </summary>
    public class HelpNoteRoundTripTests : SessionCommandFixture
    {
        private static ElementId VariableIn(Project project, string blockName, string variableName) =>
            project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == blockName)
                .Descendants().First(e => e.GetAttribute("name") == variableName).Id!.Value;

        // Both fields survive a save→reload, independently of each other.
        [Test]
        public async Task BothDocumentationFields_RoundTripIndependently()
        {
            Project project = await App.Load("testdata/projects/project2-CustomBlock.vis");
            ProjectDocumentSession session = Session(project);
            ElementId id = VariableIn(project, "Custom blok", "Tal");

            session.Apply(App.Commands.SetVariableProperties(session.Current!, id, "Tal",
                "doc text for the function documentation", ResourceInitialValue.None,
                helpNote: "help text shown to the installer"));
            Project reloaded = ProjectReader.Read(await Bytes(session.Current!));
            ProjectElement v = reloaded.FindById(id)!;

            Assert.Multiple(() =>
            {
                Assert.That(reloaded.View(v).Note, Is.EqualTo("doc text for the function documentation"),
                    "field 1 (vendor id 214) round-trips");
                Assert.That(reloaded.View(v).HelpNote, Is.EqualTo("help text shown to the installer"),
                    "field 2 (vendor id 517, note-2) round-trips");
            });
        }

        // The byte-fidelity guard: a project whose variables never had a help note must serialize UNCHANGED. If the
        // writer emitted note-2="" the file would grow an attribute the reference application never writes.
        [Test]
        public async Task WritingAnEmptyHelpNote_LeavesTheFileByteIdentical()
        {
            Project project = await App.Load("testdata/projects/project2-CustomBlock.vis");
            byte[] before = await Bytes(project);
            ProjectDocumentSession session = Session(project);
            ElementId id = VariableIn(project, "Custom blok", "Tal");

            // Re-apply the variable's EXISTING values, with an empty help note — a no-op edit.
            ProjectElement original = project.FindById(id)!;
            session.Apply(App.Commands.SetVariableProperties(session.Current!, id,
                original.GetAttribute("name") ?? "Tal", original.GetAttribute("note") ?? string.Empty,
                ResourceInitialValue.None, helpNote: string.Empty));

            byte[] after = await Bytes(session.Current!);
            Assert.Multiple(() =>
            {
                TestData.AssertBytesIdentical(before, after,
                    "an empty help note elides note-2 — the file is byte-identical");
                // NB: the .vis embeds its own DTD, which DECLARES `note-2 CDATA ""` on many elements. So the token
                // appears in every oracle; what must not appear is an ELEMENT carrying `note-2="…"`.
                Assert.That(System.Text.Encoding.Latin1.GetString(before), Does.Not.Contain("note-2=\""),
                    "…and no element in the oracle carries the attribute (only the DTD declares it)");
            });
        }

        // Clearing a help note must REMOVE the attribute again, not leave note-2="" behind.
        [Test]
        public async Task ClearingAHelpNote_RemovesTheAttributeAgain()
        {
            Project project = await App.Load("testdata/projects/project2-CustomBlock.vis");
            byte[] pristine = await Bytes(project);
            ProjectDocumentSession session = Session(project);
            ElementId id = VariableIn(project, "Custom blok", "Tal");
            string name = project.FindById(id)!.GetAttribute("name") ?? "Tal";
            string note = project.FindById(id)!.GetAttribute("note") ?? string.Empty;

            session.Apply(App.Commands.SetVariableProperties(session.Current!, id, name, note,
                ResourceInitialValue.None, helpNote: "temporary"));
            Assert.That(System.Text.Encoding.Latin1.GetString(await Bytes(session.Current!)), Does.Contain("note-2"),
                "precondition: the attribute was written");

            session.Apply(App.Commands.SetVariableProperties(session.Current!, id, name, note,
                ResourceInitialValue.None, helpNote: string.Empty));

            TestData.AssertBytesIdentical(pristine, await Bytes(session.Current!),
                "clearing it returns the file to its original bytes");
        }

        // The W5 invariant across the whole corpus: every element-level `note-2` value is preserved EXACTLY through a
        // Load→Save, and no element gains or loses one. The `project3-KompleksWired` family is the positive fixture —
        // those projects carry real vendor-authored help text, so this is not a vacuous "no occurrences" check.
        //
        // Deliberately narrower than "every oracle is a Load∘Save fixed point": `Project0-Tomt.vis` is not one, and
        // neither is the project3 family (the known catalog-enum re-hoist), both independently of this task. Asserting
        // that would invent a requirement the repo never held and blame it on W5.
        [Test]
        public async Task EveryOracle_PreservesItsHelpNotesExactly()
        {
            var seen = 0;
            foreach (string path in Directory.GetFiles(TestData.PathOf("projects"), "*.vis"))
            {
                string original = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path));
                string written = System.Text.Encoding.Latin1.GetString(await Bytes(await App.Load(path)));

                static string[] HelpNotes(string xml) => System.Text.RegularExpressions.Regex
                    .Matches(xml, "note-2=\"[^\"]*\"").Select(m => m.Value).ToArray();

                string[] before = HelpNotes(original);
                seen += before.Length;
                Assert.That(HelpNotes(written), Is.EqualTo(before),
                    $"{Path.GetFileName(path)}: every note-2 survives unchanged, and none is invented");
            }

            Assert.That(seen, Is.GreaterThan(0),
                "the corpus really does contain vendor-authored help notes — otherwise this test proves nothing");
        }
    }
}
