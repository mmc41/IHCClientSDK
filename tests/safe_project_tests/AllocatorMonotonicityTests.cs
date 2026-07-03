using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Projects.Tests
{
    /// <summary>
    /// M2 / R4 — the monotone-allocator regression net. The project id counter is a permanent high-water mark:
    /// every new element draws the next counter, and delete/undo NEVER rewinds it or reuses a retired id (spec
    /// ch. 02; experiments B4). These pins guard that <see cref="ProjectEditor.DeleteById"/> and the saved
    /// <c>last_unique_id</c> honour that rule. Uses <see cref="ProjectEditor.Group"/> (a single-id, catalog-free
    /// allocation) so the net always runs, not just when an IHC Visual install is present.
    /// </summary>
    public class AllocatorMonotonicityTests
    {
        private const string Oracle = "Project1-SimpelWired.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() => new ProjectAppService(Settings).Load("testdata/" + Oracle);

        private static long HexCounter(string? lastUniqueId) =>
            long.Parse(lastUniqueId!.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private static long CounterOf(Project project, string tag, string name) =>
            project.Root.Descendants().First(e => e.Tag == tag && e.GetAttribute("name") == name).Id!.Value.Counter;

        private static bool HasGroup(Project project, string name) =>
            project.Root.Descendants().Any(e => e.Tag == "group" && e.GetAttribute("name") == name);

        [Test]
        public async Task DeleteThenAdd_NeverReusesTheRetiredCounter()
        {
            Project project = await LoadOracle();

            // Reference: a single add establishes which counter the first new element draws (seed + 1).
            ProjectEditor reference = project.Edit();
            reference.Group("R4Reference");
            long firstCounter = CounterOf(reference.ToProject(), "group", "R4Reference");

            // Add A (draws firstCounter), delete A, add B — B must draw firstCounter + 1, NOT reuse A's counter.
            ProjectEditor editor = project.Edit();
            GroupRef a = editor.Group("R4RoomA");
            editor.RemoveGroup(a);
            editor.Group("R4RoomB");
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(CounterOf(after, "group", "R4RoomB"), Is.EqualTo(firstCounter + 1),
                    "the second add is allocated above the deleted element's counter — retired ids are never reused");
                Assert.That(HasGroup(after, "R4RoomA"), Is.False, "the deleted group is gone");
            });
        }

        [Test]
        public async Task Delete_DoesNotLowerLastUniqueId()
        {
            Project project = await LoadOracle();

            ProjectEditor adder = project.Edit();
            adder.Group("R4RoomA");
            Project afterAdd = adder.ToProject();
            long highWaterAfterAdd = HexCounter(afterAdd.LastUniqueId);

            ProjectEditor deleter = afterAdd.Edit();
            deleter.RemoveGroup(deleter.Group("R4RoomA"));
            Project afterDelete = deleter.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(highWaterAfterAdd, Is.GreaterThan(HexCounter(project.LastUniqueId)), "adding raised the high-water");
                Assert.That(HexCounter(afterDelete.LastUniqueId), Is.EqualTo(highWaterAfterAdd),
                    "deleting does NOT lower last_unique_id — the counter is a permanent high-water");
                Assert.That(HasGroup(afterDelete, "R4RoomA"), Is.False);
            });
        }

        [Test]
        public async Task AddThenDelete_BurnsTheCounter_LeavingAPermanentHole()
        {
            Project project = await LoadOracle();

            // A plain single add raises the high-water to R.
            ProjectEditor reference = project.Edit();
            reference.Group("R4Reference");
            long highWaterAfterOneAdd = HexCounter(reference.ToProject().LastUniqueId);

            // Add-then-delete leaves the SAME high-water — the id is burned, not rewound.
            ProjectEditor editor = project.Edit();
            GroupRef x = editor.Group("R4RoomX");
            editor.RemoveGroup(x);
            Project after = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(HexCounter(after.LastUniqueId), Is.EqualTo(highWaterAfterOneAdd),
                    "add-then-delete leaves the same high-water as a plain add — the counter is burned, not rewound");
                Assert.That(HasGroup(after, "R4RoomX"), Is.False, "the group is gone, but its counter is retired (a permanent hole)");
            });
        }
    }
}
