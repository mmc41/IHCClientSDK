using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Projects;
using ihc_openvisual.ViewModels;

namespace Ihc.Vis.Tests;

/// <summary>
/// The labels deep in the function-block subtree must read as IHC Visual renders them (uxparity S-33).
/// All three cases here were OpenVisual dropping information the file holds: a scene's note, the section
/// caption stored on the container, and the millisecond part of a time setting.
/// </summary>
public class DeepTreeLabelParityTests
{
    /// <summary>
    /// The projector over the oracle project, reached WITHOUT a shell. Every assertion below is about what the
    /// projector renders, so a harness would be scenery: it would make each case look like a statement about the
    /// application when it is a statement about one class.
    /// </summary>
    private static async Task<TreeNodeViewModel> FunctionsRootAsync()
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis");
        Project project = await new ProjectAppService(new Ihc.IhcSettings()).Load(path);
        return new ProjectTreeProjector(project).BuildLocalitiesRoot(functions: true);
    }

    private static TreeNodeViewModel Find(TreeNodeViewModel node, string label) =>
        node.DisplayName == label ? node : node.Children.Select(c => Find(c, label)).FirstOrDefault(n => n is not null)!;

    private static TreeNodeViewModel FirstBlock(TreeNodeViewModel functionsRoot) =>
        functionsRoot.Children.First(l => l.Children.Count > 0).Children[0];

    [Test]
    public async Task ScenePin_ShowsItsNote_TruncatedTo15Characters()
    {
        TreeNodeViewModel root = await FunctionsRootAsync();

        // "Fremkalder scenarie ved tænding." -> the first 15 characters, then an ellipsis.
        Assert.That(Find(root, "Scenarie Tænd (Fremkalder scen...)"), Is.Not.Null,
            "a scene row carries its note; a note longer than 15 characters is cut and elided");
    }

    [Test]
    public async Task NonScenePin_WithANote_ShowsNoSuffix()
    {
        TreeNodeViewModel root = await FunctionsRootAsync();

        // "Kip" has a note too, but only scene rows put it in the label.
        Assert.That(Find(root, "Kip"), Is.Not.Null, "an input pin renders its bare name even though it has a note");
    }

    [Test]
    public async Task Section_ShowsTheCaptionStoredInTheProject()
    {
        TreeNodeViewModel root = await FunctionsRootAsync();

        var sections = FirstBlock(root).Children.Select(c => c.DisplayName).ToArray();
        Assert.That(sections, Is.EqualTo(new[] { "Input", "Output", "Indstillinger" }),
            "the caption comes from the container's own name, not a hard-coded English one");
    }

    [Test]
    public async Task TimeSetting_ShowsMillisecondsWithADecimalComma()
    {
        TreeNodeViewModel root = await FunctionsRootAsync();

        Assert.That(Find(root, "Timer = 00:03:00,000"), Is.Not.Null,
            "a time setting renders to milliseconds, comma-separated");
    }
}
