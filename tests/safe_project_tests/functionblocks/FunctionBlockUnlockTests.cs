using System.Linq;
using System.Threading.Tasks;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// BL-8 — <see cref="FunctionBlockRef.Unlock"/>, the inverse of <see cref="FunctionBlockRef.Locked"/> (US-020
    /// "Oplås"): demote a block loaded with <c>locked="yes"</c> to an editable custom block by clearing the flag to
    /// its default, so the canonicalizer omits it on save. Oracle: the locked <c>AutoProof</c> block in
    /// <c>project2-CustomBlock.vis:451</c>. (The GUI icon swap on unlock is out of scope per §2.3.)
    /// </summary>
    public class FunctionBlockUnlockTests
    {
        private const string Oracle = "project2-CustomBlock.vis";
        private static IhcSettings Settings => TestSetup.Settings;

        private static Task<Project> LoadOracle() =>
            new ProjectAppService(Settings).Load("testdata/projects/" + Oracle);

        private static FunctionBlockRef Block(ProjectEditor editor, Project project, string name)
        {
            ProjectElement fb = project.Root.Descendants().First(e => e.Tag == "functionblock" && e.GetAttribute("name") == name);
            string groupName = project.FindParent(fb.Id!.Value)!.GetAttribute("name")!;
            return editor.Group(groupName).FunctionBlock(name);
        }

        [Test]
        public async Task Unlock_DemotesLoadedLockedBlock_OmittingTheLockedFlag()
        {
            Project project = await LoadOracle();
            var app = new ProjectAppService(Settings);
            ProjectElement locked = project.Root.Descendants().First(e => e.GetAttribute("name") == "AutoProof");
            Assert.That(locked.GetAttribute("locked"), Is.EqualTo("yes"), "precondition: the block is loaded locked");

            ProjectEditor editor = project.Edit();
            Block(editor, project, "AutoProof").Unlock();
            Project built = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(built.FindById(locked.Id!.Value)!.GetAttribute("locked"), Is.Null,
                    "locked is cleared to its default and omitted — the block is now editable");
                Assert.That(app.Validate(built).IsValid, Is.True);
            });
        }

        [Test]
        public async Task Unlock_ReturnsHandle_ForChaining()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            FunctionBlockRef block = Block(editor, project, "AutoProof");

            Assert.That(block.Unlock(), Is.SameAs(block));
        }

        [Test]
        public async Task Locked_ThenUnlock_IsANoOpOnAnAlreadyEditableBlock()
        {
            Project project = await LoadOracle();
            ProjectEditor editor = project.Edit();
            FunctionBlockRef custom = Block(editor, project, "Custom blok");   // not locked

            custom.Locked().Unlock();
            Project built = editor.ToProject();

            ProjectElement fb = built.Root.Descendants().First(e => e.GetAttribute("name") == "Custom blok");
            Assert.That(fb.GetAttribute("locked"), Is.Null, "lock then unlock leaves no locked attribute");
        }
    }
}
