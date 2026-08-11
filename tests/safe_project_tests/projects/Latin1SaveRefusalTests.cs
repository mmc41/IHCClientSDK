using System;
using System.IO;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The registered difference "refuses to save text the <c>.vis</c> character repertoire cannot store — naming
    /// the offending element and character — where the original writes an unparsable file".
    ///
    /// <para>The format is ISO-8859-1 with no BOM, so a character outside U+0000..U+00FF simply has no
    /// representation. Writing it anyway produces a file neither application can read back, and the loss is silent
    /// until someone opens the project again. Refusing is therefore the enhancement, and the refusal is only useful
    /// if it says WHERE: one bad character in a 200 KB project is otherwise unfindable by hand.</para>
    ///
    /// <para>What must not drift: the save is refused (nothing written), and the message names the element, its id
    /// and the offending code point. A refusal that just says "encoding error" would satisfy the first half of the
    /// register entry and quietly drop the second.</para>
    /// </summary>
    public class Latin1SaveRefusalTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        // U+20AC EURO SIGN — not in Latin-1, and an entirely plausible thing to type into a note.
        private const string OutsideLatin1 = "Pris: 20€";

        private static async Task<Project> ProjectWithNonLatin1NoteAsync()
        {
            Project project = await new ProjectAppService(Settings).Load("testdata/projects/Project0-Tomt.vis");
            ProjectEditor editor = project.Edit();
            editor.Group("Stue").Note(OutsideLatin1);
            return editor.ToProject();
        }

        [Test]
        public async Task Saving_TextOutsideTheRepertoire_IsRefused_NamingTheElementAndTheCharacter()
        {
            Project project = await ProjectWithNonLatin1NoteAsync();
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"latin1-{Guid.NewGuid():N}.vis");

            var refusal = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await new ProjectAppService(Settings).Save(project, path));

            Assert.Multiple(() =>
            {
                Assert.That(refusal!.Message, Does.Contain("ISO-8859-1"), "the refusal names the repertoire");
                Assert.That(refusal.Message, Does.Contain("<group>"), "and the element that carries the text");
                Assert.That(refusal.Message, Does.Contain("'note'"), "and which of its attributes");
                Assert.That(refusal.Message, Does.Contain("U+20AC"), "and the character itself, by code point");
                Assert.That(File.Exists(path), Is.False,
                    "nothing is written — a partially written .vis is exactly the unparsable file this avoids");
            });
        }

        /// <summary>The boundary the repertoire actually draws: U+00FF is the last storable character and saves
        /// normally, so the refusal is about representability and not about "unusual" text. The Danish letters and
        /// the accent range these projects are full of are all below it.</summary>
        [Test]
        public async Task Saving_TheLastLatin1Character_IsAccepted()
        {
            Project project = await new ProjectAppService(Settings).Load("testdata/projects/Project0-Tomt.vis");
            ProjectEditor editor = project.Edit();
            editor.Group("Stue").Note("æøå ÆØÅ ÿ");
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"latin1-ok-{Guid.NewGuid():N}.vis");

            await new ProjectAppService(Settings).Save(editor.ToProject(), path);

            Assert.That(File.Exists(path), Is.True, "U+00FF is inside the repertoire, so it stores");
            File.Delete(path);
        }
    }
}
