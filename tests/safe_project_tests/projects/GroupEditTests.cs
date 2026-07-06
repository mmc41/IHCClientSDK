using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// M4 / 4.2 — editing a seed room's identity (rename + note). project3 renames the default "Stue" to
    /// "Stue &amp; Køkken &quot;åben&quot;" and adds a note carrying the full Latin-1 accent range; both are plain
    /// attribute edits, so they allocate nothing and reuse no id (R3). Catalog-free (loads an authored oracle).
    /// </summary>
    public class GroupEditTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        [Test]
        public async Task Group_RenameAndNote_SetsAttributes_AllocatesNothing()
        {
            Project project = await new ProjectAppService(Settings).Load("testdata/projects/Project0-Tomt.vis");
            string highWater = project.LastUniqueId!;
            ProjectEditor editor = project.Edit();

            editor.Group("Stue").Name("Stue & Køkken \"åben\"").Note("note æøå ÆØÅ ÄÖ é©®");

            Project after = editor.ToProject();
            ProjectElement room = after.Groups.First(g => g.GetAttribute("name") == "Stue & Køkken \"åben\"");
            Assert.Multiple(() =>
            {
                Assert.That(room.GetAttribute("note"), Is.EqualTo("note æøå ÆØÅ ÄÖ é©®"), "the note is set");
                Assert.That(after.LastUniqueId, Is.EqualTo(highWater), "an attribute edit allocates nothing (R3)");
            });
        }
    }
}
