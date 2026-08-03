using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// W4 / RC3 (uxparity2 T018): a block may hold MORE THAN ONE program, and creating one is a missing SDK command —
    /// not a missing menu entry. `project2-CustomBlock.vis` proves the multi-program shape is real and vendor-authored:
    /// its locked `AutoProof` block carries two `program_simple` children (`Sluk`, `Timertid`).
    /// <para>
    /// A new program is only useful if it round-trips, so each test writes the edited project and reads it back rather
    /// than asserting over the in-memory tree alone.
    /// </para>
    /// </summary>
    public class AddProgramCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        private static ElementId ProgramsOf(Project project, string blockName) =>
            project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == blockName)
                .ChildrenOrEmpty().First(c => c.Tag == "programs").Id!.Value;

        private static async Task<Project> RoundTrip(Project project)
        {
            using var ms = new MemoryStream();
            await App.Save(project, ms);
            return ProjectReader.Read(ms.ToArray());
        }

        [Test]
        public async Task AddProgram_CreatesAProgramWithItsEventsAndCommands_AndRoundTrips()
        {
            Project project = await Load("project2-CustomBlock.vis");
            var app = App;
            ProjectDocumentSession session = Session(project);
            ElementId programs = ProgramsOf(project, "Custom blok");
            int before = project.FindById(programs)!.ChildrenOrEmpty().Count(c => c.Tag == "program_simple");

            session.Apply(app.Commands.AddProgram(session.Current!, programs, "Nyt program"));
            Project reloaded = await RoundTrip(session.Current!);

            ProjectElement added = reloaded.Root.Descendants()
                .Where(e => e.Tag == "program_simple" && e.GetAttribute("name") == "Nyt program").Single();

            Assert.Multiple(() =>
            {
                Assert.That(reloaded.FindById(ProgramsOf(reloaded, "Custom blok"))!.ChildrenOrEmpty()
                        .Count(c => c.Tag == "program_simple"), Is.EqualTo(before + 1),
                    "exactly one program was added");
                Assert.That(added.ChildrenOrEmpty().Select(c => c.Tag), Is.EqualTo(new[] { "events", "actions" }),
                    "a program owns an events container and a commands container, in that order");
                Assert.That(added.GetAttribute("icon"), Is.EqualTo("_0x7"), "the vendor program icon");
                Assert.That(added.ChildrenOrEmpty().First(c => c.Tag == "actions").GetAttribute("type"),
                    Is.EqualTo("_0x2"), "the root commands container carries the vendor branch type");
            });
        }

        // The new program must be structurally indistinguishable from one the vendor authored — otherwise a project
        // containing it would not survive a vendor round-trip. AutoProof's two programs are the oracle.
        [Test]
        public async Task AddedProgram_MatchesTheVendorAuthoredShape()
        {
            Project project = await Load("project2-CustomBlock.vis");
            var app = App;
            ProjectDocumentSession session = Session(project);
            session.Apply(app.Commands.AddProgram(session.Current!, ProgramsOf(project, "Custom blok"), "Nyt program"));
            Project reloaded = await RoundTrip(session.Current!);

            ProjectElement vendor = reloaded.Root.Descendants()
                .First(e => e.Tag == "program_simple" && e.GetAttribute("name") == "Sluk");        // vendor-authored
            ProjectElement added = reloaded.Root.Descendants()
                .First(e => e.Tag == "program_simple" && e.GetAttribute("name") == "Nyt program"); // ours

            static (string? name, string? icon, string? note, string? type) Shape(ProjectElement e, string tag)
            {
                ProjectElement c = e.ChildrenOrEmpty().First(x => x.Tag == tag);
                return (c.GetAttribute("name"), c.GetAttribute("icon"), c.GetAttribute("note"), c.GetAttribute("type"));
            }

            Assert.Multiple(() =>
            {
                Assert.That(added.GetAttribute("icon"), Is.EqualTo(vendor.GetAttribute("icon")), "program icon");
                Assert.That(Shape(added, "events"), Is.EqualTo(Shape(vendor, "events")), "the events container");
                Assert.That(Shape(added, "actions"), Is.EqualTo(Shape(vendor, "actions")), "the commands container");
            });
        }

        // Placement is the engine's rule: a program belongs under a `programs` container and nowhere else.
        [Test]
        public async Task AddProgram_IsRefused_AwayFromAProgramsContainer()
        {
            Project project = await Load("project2-CustomBlock.vis");
            var app = App;
            ElementId inputs = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok")
                .ChildrenOrEmpty().First(c => c.Tag == "inputs").Id!.Value;

            EditVerdict verdict = app.CanApply(project, app.Commands.AddProgram(project, inputs, "Nyt program"));

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False, "an Input section holds variables, not programs");
                Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty, "…and the refusal says why");
            });
        }

        // A-27: a locked library block is view-only, so its program list cannot be extended either.
        [Test]
        public async Task AddProgram_IsRefused_InsideALockedBlock()
        {
            Project project = await Load("project2-CustomBlock.vis");
            var app = App;

            EditVerdict verdict = app.CanApply(project,
                app.Commands.AddProgram(project, ProgramsOf(project, "AutoProof"), "Nyt program"));

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Ok, Is.False, "AutoProof is locked");
                Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty);
            });
        }
    }
}
