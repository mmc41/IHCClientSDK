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

        [Test]
        public void Group_ExistingRoomWithUnparseableId_Throws_NotDuplicate()
        {
            // Finding 12: an open-world room whose id token is out of the parseable range (> 0xFFFFFFFF) has a null
            // Id. Group("Stue") must throw (unaddressable), never silently seed a second "Stue" that later inserts
            // would land in.
            Project project = new Project(Tree.Node("utcs_project", null,
                new[] { ("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x6000") },
                Tree.Node("groups", "_0x2031", new[] { ("name", "L") },
                    Tree.Node("group", "_0x1ffffffff", new[] { ("name", "Stue") }))));

            ProjectEditor editor = project.Edit();

            Assert.That(() => editor.Group("Stue"),
                Throws.InvalidOperationException.With.Message.Contains("not a parseable"),
                "an unaddressable existing room fails loudly instead of seeding a duplicate");
        }
    }
}
