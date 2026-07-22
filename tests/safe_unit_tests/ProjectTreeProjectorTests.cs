using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace safe_unit_tests;

/// <summary>
/// T002 characterization: pins the <see cref="ProjectTreeProjector"/> function-block rendering that the
/// programming-mode gap review found ALREADY-CORRECT (PG-C1, PG-C3), so a future change cannot silently regress it.
/// The projector is Avalonia-free (project in, nodes out), so these run headlessly here — no App needed.
/// <list type="bullet">
/// <item>PG-C1 — configuration mode hides empty FB sections AND the <c>Internal variables</c> section, while
/// programming mode shows all four sections.</item>
/// <item>PG-C3 — a sub-program's row label is its user-set name, falling back to the default token
/// (<c>"Sub-program"</c>) only when the stored name is absent or the vendor default <c>"Under program"</c>.</item>
/// </list>
/// The custom-sub-program-name branch (PG-C3) is exercised against the <c>Project1-SimpelWired</c> oracle, which
/// carries both user-named sub-programs (e.g. <c>"Start blokkering"</c>) and default ones — there is no public
/// rename-sub-program command to synthesise a custom name programmatically. That same oracle's FBs carry a
/// populated <c>internalsettings</c> section, so its absence in configuration mode proves the section is hidden by
/// MODE, not merely because it is empty. The empty-section half of PG-C1 uses a from-scratch ("Tom blok") block,
/// whose four sections are present but childless.
/// </summary>
public class ProjectTreeProjectorTests
{
    private static readonly string[] AllFourSections = ["Input", "Output", "Settings", "Internal variables"];
    private static readonly string[] NonInternalSections = ["Input", "Output", "Settings"];

    private static ProjectAppService Service() => new(new IhcSettings());

    private static Task<Project> Project1Oracle() =>
        Service().Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "Project1-SimpelWired.vis"));

    // The function blocks of a project, reached exactly as the projector reaches them (direct children of a locality).
    private static IEnumerable<ProjectElement> FunctionBlocks(Project project) =>
        project.Groups.SelectMany(g => g.ChildrenOrEmpty()).Where(c => c.Kind == ElementKind.FunctionBlock);

    private static IEnumerable<TreeNodeViewModel> Flatten(TreeNodeViewModel node)
    {
        yield return node;
        foreach (TreeNodeViewModel child in node.Children)
        {
            foreach (TreeNodeViewModel descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    // ---- PG-C1: function-block variable-section visibility differs by mode ----

    [Test]
    public void FunctionBlockNode_ConfigurationMode_HidesEmptySections_ProgrammingMode_ShowsAllFour()
    {
        // A from-scratch ("Tom blok") block has all four sections present but childless.
        ProjectAppService service = Service();
        Project project = service.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
        ElementId localityId = project.Groups[0].Id!.Value;
        var session = new ProjectDocumentSession();
        session.Open(project);
        EditOutcome<ElementId> added =
            session.Apply(service.Commands.AddEmptyFunctionBlock(session.Current!, localityId, "Tom blok"));
        Project withBlock = session.Current!;
        ProjectElement emptyBlock = withBlock.FindById(added.Value)!;

        var projector = new ProjectTreeProjector(withBlock);
        TreeNodeViewModel config = projector.BuildFunctionBlockNode(emptyBlock, "Tom blok", programmingMode: false);
        TreeNodeViewModel programming = projector.BuildFunctionBlockNode(emptyBlock, "Tom blok", programmingMode: true);

        Assert.Multiple(() =>
        {
            Assert.That(added.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(config.Children, Is.Empty,
                "configuration mode hides all four sections when the block is empty");
            Assert.That(programming.Children.Select(c => c.DisplayName), Is.EqualTo(AllFourSections),
                "programming mode shows all four sections even when they are empty");
        });
    }

    [Test]
    public async Task FunctionBlockNode_PopulatedBlock_ConfigurationModeHidesInternalVariablesEvenWhenNonEmpty()
    {
        // Both Project1 FBs carry a populated internalsettings section; its absence in configuration mode proves the
        // section is hidden by mode, not because it happens to be empty.
        Project project = await Project1Oracle();
        ProjectElement fb = FunctionBlocks(project).First();
        var projector = new ProjectTreeProjector(project);

        TreeNodeViewModel config = projector.BuildFunctionBlockNode(fb, "FB", programmingMode: false);
        TreeNodeViewModel programming = projector.BuildFunctionBlockNode(fb, "FB", programmingMode: true);

        Assert.Multiple(() =>
        {
            Assert.That(config.Children.Select(c => c.DisplayName), Is.EqualTo(NonInternalSections),
                "configuration mode shows the populated non-internal sections and hides Internal variables");
            Assert.That(programming.Children.Select(c => c.DisplayName), Is.EqualTo(AllFourSections),
                "programming mode adds the populated Internal variables section");
        });
    }

    // ---- PG-C3: a sub-program's label is its user name, else the default token ----

    [Test]
    public async Task SubProgramNode_RendersUserSetName_FallsBackToDefaultTokenOtherwise()
    {
        Project project = await Project1Oracle();
        var projector = new ProjectTreeProjector(project);

        List<string> subProgramLabels = FunctionBlocks(project)
            .Select(fb => projector.BuildBlockProgramsNode(fb, "FB"))
            .SelectMany(Flatten)
            .Where(n => n.Kind == TreeNodeKind.SubProgram)
            .Select(n => n.DisplayName)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(subProgramLabels, Is.Not.Empty, "the oracle FBs contain sub-programs");
            Assert.That(subProgramLabels, Has.Member("Start blokkering"),
                "a sub-program with a user-set name renders that name verbatim");
            Assert.That(subProgramLabels, Has.Member("Sub-program"),
                "a default ('Under program') / unnamed sub-program falls back to the default token");
        });
    }
}
