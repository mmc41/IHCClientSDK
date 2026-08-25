using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-11: menu separators must reach the automation tree.
///
/// <para>Grouping is meaning. The reference application closes every node flyout with a rule before
/// <i>Egenskaber</i>, and its File menu groups the file commands, the recent list and Luk into three blocks —
/// and it publishes those rules to automation, where they read back as empty-label rows. OpenVisual draws the
/// same rules but Avalonia's stock <c>Separator</c> has no automation peer, so nothing downstream can perceive
/// them: a screen-reader user hears one undifferentiated list of items (accessibility, checklist dim 19), and a
/// menu dump shows no separator at all — which made a correctly-grouped OpenVisual menu diff against the
/// vendor as though the grouping were missing (checklist dims 7/8).</para>
///
/// <para>The assertion is on the PEER rather than on the XAML: a <c>Separator</c> element being present in the
/// markup is exactly what was already true while nothing could see it.</para>
/// </summary>
public class MenuSeparatorAccessibilityTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task FileMenu_Separators_ReachTheAutomationTree()
    {
        (MainWindow window, _) = await ShowShellAsync();

        var file = window.GetVisualDescendants().OfType<MenuItem>()
            .Single(m => AutomationProperties.GetAutomationId(m) == "MenuFile");
        var separators = file.Items.OfType<Separator>().ToList();

        Assert.That(separators, Is.Not.Empty, "the File menu groups its commands with rules");
        foreach (Separator separator in separators)
        {
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(separator);
            Assert.That(peer, Is.Not.Null, "a separator that has no peer cannot be perceived at all");
            Assert.That(peer.GetAutomationControlType(), Is.EqualTo(AutomationControlType.Separator),
                "a rule must announce itself AS a separator — the role is what carries 'a group ends here'");
        }
    }

    /// <summary>
    /// The node flyout's rule is the one the comparison turns on: it is visibility-gated on Egenskaber, so it
    /// must be both PRESENT and perceivable wherever Egenskaber is offered.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task NodeContextFlyout_ClosesWithAPerceivableRule()
    {
        (MainWindow window, MainWindowViewModel viewModel) = await ShowShellAsync();
        viewModel.SelectNode(viewModel.InstallationNodes[0].Children[0]);   // a locality: Egenskaber is offered

        var flyout = (MenuFlyout)window.Resources["NodeContextMenu"]!;
        var separators = flyout.Items.OfType<Separator>().ToList();

        Assert.That(separators, Is.Not.Empty, "the node flyout closes with a rule before Egenskaber");
        foreach (Separator separator in separators)
        {
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(separator);
            Assert.That(peer, Is.Not.Null);
            Assert.That(peer.GetAutomationControlType(), Is.EqualTo(AutomationControlType.Separator),
                "the flyout's rule is perceivable, so a dump of this menu can be compared for GROUPING");
        }
    }

    private static async Task<(MainWindow, MainWindowViewModel)> ShowShellAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        return (window, viewModel);
    }
}
