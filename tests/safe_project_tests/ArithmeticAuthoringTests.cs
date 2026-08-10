using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Programs;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// W3 / F3 (uxparity2 T023): authoring one arithmetic command through the command layer writes the exact
    /// <c>&lt;action&gt;</c> element the reference application writes, with the opcode the F-108 grid dictates for the
    /// (operator, target-type, operand-type) triple.
    /// <para>
    /// F3 recorded arithmetic as a no-op that "returns Ok and produces no row". T009 re-measured it live and found
    /// the opposite: invoking an operand LEAF authors the row and persists it, while invoking the submenu CATEGORY
    /// HEADER does nothing — the header carries no command by construction. These tests pin the working behaviour at
    /// the engine boundary; the GUI half (leaf authors, header is inert) is pinned in safe_visual_tests.
    /// </para>
    /// </summary>
    public class ArithmeticAuthoringTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        private static ElementId Find(Project project, string name) =>
            project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok")
                .Descendants().First(e => e.GetAttribute("name") == name).Id!.Value;

        // The element T009 measured coming out of the live application, reproduced through the command layer.
        [Test]
        public async Task ArithmeticCommand_WritesTheVendorActionElement_WithTheGridOpcode()
        {
            Project project = await App.Load("testdata/projects/project2-CustomBlock.vis");
            ProjectDocumentSession session = Session(project);
            ElementId commands = project.Root.Descendants()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok")
                .Descendants().First(e => e.Tag == "actions").Id!.Value;
            ElementId target = Find(project, "Tal");        // resource_integer
            ElementId operand = Find(project, "Kommatal");  // resource_floating_point

            string method = ProgramMethodCatalog.ArithmeticToken("+", "resource_integer", "resource_floating_point")!;
            session.Apply(App.Commands.AddArithmeticCommand(session.Current!, commands, target, method, operand,
                "%P = %P + %S", "Sætter %P til sin egen værdi plus %S"));

            using var ms = new MemoryStream();
            await App.Save(session.Current!, ms);
            Project reloaded = ProjectReader.Read(ms.ToArray());
            ProjectElement action = reloaded.Root.Descendants()
                .Where(e => e.Tag == "action" && e.GetAttribute("method") == method).Single();

            Assert.Multiple(() =>
            {
                Assert.That(method, Is.EqualTo("_0x5f"), "the mixed-column opcode for (int += float)");
                Assert.That(action.GetAttribute("name"), Is.EqualTo("%P = %P + %S"),
                    "the vendor template, placeholders left live so the row re-renders on rename");
                Assert.That(action.GetAttribute("note"), Is.EqualTo("Sætter %P til sin egen værdi plus %S"),
                    "the vendor note is part of the persisted action payload");
                Assert.That(action.GetAttribute("icon"), Is.EqualTo("_0x9"), "the vendor action icon");
                Assert.That(action.GetAttribute("link1"), Is.EqualTo(reloaded.FindById(target)!.GetAttribute("id")),
                    "link1 is the TARGET register");
                Assert.That(action.GetAttribute("link2"), Is.EqualTo(reloaded.FindById(operand)!.GetAttribute("id")),
                    "link2 is the OPERAND");
            });
        }

        // Every authorable cell of the grid writes its own opcode — so the command layer never invents one, and a
        // dead cell has no token to write in the first place (T008: 15 live of 36).
        [Test]
        public async Task EveryAuthorableGridCell_HasAnOpcode_AndEveryDeadCellHasNone()
        {
            string[] tags = ["resource_floating_point", "resource_integer", "resource_counter"];
            var live = 0;
            var dead = 0;
            foreach (string op in new[] { "+", "-", "/", "*" })
            {
                foreach (string t in tags)
                {
                    foreach (string o in tags)
                    {
                        if (ProgramMethodCatalog.ArithmeticToken(op, t, o) is { } token)
                        {
                            live++;
                            Assert.That(token, Does.StartWith("_0x"), $"({op},{t},{o}) yields a vendor opcode");
                        }
                        else
                        {
                            dead++;
                        }
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(live, Is.EqualTo(15), "the authorable cells (uxparity2 V5 grid)");
                Assert.That(dead, Is.EqualTo(21), "…and the dead ones, which are never offered");
            });
            await Task.CompletedTask;
        }

        [Test]
        public void ArithmeticNote_UsesTheVendorPayloadForTheConcreteOperandDirection()
        {
            const string flt = "resource_floating_point";
            const string integer = "resource_integer";

            Assert.Multiple(() =>
            {
                Assert.That(ProgramMethodCatalog.ArithmeticNote("+", integer, flt),
                    Is.EqualTo("Sætter %P til sin egen værdi plus %S"));
                Assert.That(ProgramMethodCatalog.ArithmeticNote("-", flt, integer),
                    Is.EqualTo("Sætter %P til sin egen værdi minus %S"));
                Assert.That(ProgramMethodCatalog.ArithmeticNote("/", integer, flt),
                    Is.EqualTo("Tilskrev værdien %P med %S"));
                Assert.That(ProgramMethodCatalog.ArithmeticNote("*", flt, flt),
                    Is.EqualTo("Sætter %P til sin egen værdi ganget med %S"));
                Assert.That(ProgramMethodCatalog.ArithmeticNote("*", flt, integer),
                    Is.EqualTo("Sætter %P til sin egen værdi ganget med %S"));
                Assert.That(ProgramMethodCatalog.ArithmeticNote("*", integer, flt),
                    Is.EqualTo("Tilskrev værdien %P til %S"));
                Assert.That(ProgramMethodCatalog.ArithmeticNote("*", integer, integer), Is.Null,
                    "a dead grid cell has no payload to persist");
            });
        }
    }
}
