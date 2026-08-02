using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-9: the programming-authoring command family — a representative authoring edit byte-round-trips
    /// against the engine, ToggleLogMark is reversible, and a command aimed at the wrong container is Refused.
    /// </summary>
    public class ProgramCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        // T037: each authorable arithmetic cell (incl. the mixed-conversion codes _0x5f/_0x69/_0x6e/_0x78) authors via
        // the engine with the F-108 grid opcode and survives a save→reload byte round-trip.
        [Test]
        public async Task Arithmetic_AuthorableCells_AuthorWithGridOpcodesAndRoundTrip()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ProjectEditor editor = project.Edit();
            FunctionBlockRef fb = editor.Group("Stue").FunctionBlock("Custom blok");   // unlocked custom block
            ResourceRef intT = fb.AddSetting("resource_integer", "IntT");
            ResourceRef intO = fb.AddSetting("resource_integer", "IntO");
            ResourceRef fltT = fb.AddSetting("resource_floating_point", "FltT");
            ResourceRef fltO = fb.AddSetting("resource_floating_point", "FltO");
            BranchRef branch = fb.Program().AddSubProgram().WhenTrue;

            string add = ProgramMethodCatalog.ArithmeticToken("+", "resource_integer", "resource_floating_point")!;        // _0x5f
            string sub = ProgramMethodCatalog.ArithmeticToken("-", "resource_floating_point", "resource_integer")!;        // _0x69
            string div = ProgramMethodCatalog.ArithmeticToken("/", "resource_integer", "resource_integer")!;              // _0x6e
            string mul = ProgramMethodCatalog.ArithmeticToken("*", "resource_floating_point", "resource_floating_point")!; // _0x78
            branch.AddAction("%P = %P + %S", intT, add, fltO);
            branch.AddAction("%P = %P - %S", fltT, sub, intO);
            branch.AddAction("%P = %P / %S", intT, div, intO);
            branch.AddAction("%P = %P * %S", fltT, mul, fltO);

            using var ms = new MemoryStream();
            await new ProjectAppService(TestSetup.Settings).Save(editor.ToProject(), ms);
            var authored = ProjectReader.Read(ms.ToArray()).Root.Descendants()
                .Where(e => e.Tag == "action" && e.GetAttribute("method") is not null)
                .Select(e => e.GetAttribute("method")).ToList();

            Assert.Multiple(() =>
            {
                Assert.That((add, sub, div, mul), Is.EqualTo(("_0x5f", "_0x69", "_0x6e", "_0x78")), "the grid opcodes");
                Assert.That(authored, Does.Contain("_0x5f").And.Contain("_0x69").And.Contain("_0x6e").And.Contain("_0x78"),
                    "every authored cell survives the save→reload round-trip");
            });
        }

        [Test]
        public async Task AddSubProgram_OnCommandContainer_MatchesEngine()
        {
            Project loaded = await Load("project3-KompleksWired.vis");
            // project3's first command container lives in a library-locked block; unlock the blocks so both the
            // session and engine paths can author into it (T003 refuses authoring into a locked block). Id-neutral.
            ProjectEditor prep = loaded.Edit();
            foreach (ElementId fbId in loaded.Root.Descendants()
                         .Where(e => e.Tag == "functionblock").Select(e => e.Id!.Value).ToList())
            {
                prep.FunctionBlock(fbId).Unlock("Test Installer", new DateOnly(2026, 1, 1));
            }
            Project project = prep.ToProject();
            ProjectElement actions = project.Root.Descendants().First(e => e.Tag == "actions" && e.Id is not null);
            ElementId id = actions.Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new AddSubProgram(id));

            ProjectEditor editor = project.Edit();
            editor.Branch(id).AddSubProgram();
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own Branch.AddSubProgram");
            });
        }

        [Test]
        public async Task AddSubProgram_OnWrongContainer_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;   // not an "actions" container
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new AddSubProgram(locality));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task ToggleLogMark_IsReversible()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement logRow = project.Root.Descendants()
                .First(e => e.Id is not null && e.IsLogRow(project));
            ElementId id = logRow.Id!.Value;
            ProjectDocumentSession session = Session(project);
            Project before = session.Current!;

            EditOutcome toggled = session.Apply(new ToggleLogMark(id));
            session.Undo();

            Assert.Multiple(() =>
            {
                Assert.That(toggled.Status, Is.EqualTo(EditStatus.Committed), "the toggle changed the log mark");
                Assert.That(session.Current!.Equals(before), Is.True, "toggle then undo returns to the original");
            });
        }
    }
}
