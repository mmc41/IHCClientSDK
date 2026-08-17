using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-5 and F-45 — the toolbar's GROUPING: how many rules it draws, where, and whether anything but a
/// sighted user can perceive them.
///
/// <para><b>F-5, measured 2026-08-10 via the vendor's own <c>toolbar.dump</c></b> (12 entries read from the live
/// TBBUTTON array, every id resolved through the menu): the reference application's bar carries exactly <b>one</b>
/// separator, after <i>Gem projekt</i>, and nothing else — Om, Hent, Send, Klip, Kopier, Indsæt and the two
/// simulation buttons run on unbroken. OpenVisual drew three (after Gem, after Hjælp, after Send).</para>
///
/// <para><b>F-45</b>: OpenVisual's toolbar rules were <see cref="Avalonia.Controls.Shapes.Rectangle"/>s, which
/// publish nothing to automation — the same defect F-11 fixed for the MENUS, in the surface nobody re-checked
/// afterwards. Grouping is meaning: a rule says "these belong together and those do not", and a shape says that to
/// sighted users only. It also made the divergence above unmeasurable from the OpenVisual side, which is why F-5
/// sat open on a vendor-side measurement for a whole campaign.</para>
/// </summary>
public class ToolbarSeparatorParityTests : AvaloniaTestBase
{
    /// <summary>The vendor's bar, as its toolbar.dump read it — minus the two simulation buttons, which OpenVisual
    /// omits under the registered simulation-out-of-scope difference. The separator's POSITION is the point: a
    /// count alone would pass on three rules collapsed into one wrong place.</summary>
    private static readonly string[] ExpectedOrder =
    [
        "ToolbarNew", "ToolbarOpen", "ToolbarSave",
        "|",
        "ToolbarHelp", "ToolbarRetrieve", "ToolbarSend", "ToolbarCut", "ToolbarCopy", "ToolbarPaste",
    ];

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheToolbarGroupsAsTheReferenceApplicationDoes()
    {
        MainWindow window = await ShowShellAsync();

        Assert.That(BarLayout(window), Is.EqualTo(ExpectedOrder).AsCollection,
            "the reference application's bar carries ONE rule, after Gem projekt");
    }

    /// <summary>Every rule must publish itself as a Separator. Asserted on the PEER, not on the element type: a
    /// separator being present in the markup is exactly what was already true while nothing could perceive it
    /// (the F-11 lesson, which this surface never had applied to it).</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task EveryToolbarRule_PublishesItselfAsASeparator()
    {
        MainWindow window = await ShowShellAsync();

        IReadOnlyList<Control> rules = Rules(window);
        Assert.That(rules, Is.Not.Empty, "the toolbar draws at least one rule to publish");
        Assert.Multiple(() =>
        {
            foreach (Control rule in rules)
            {
                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(rule);
                Assert.That(peer.GetAutomationControlType(), Is.EqualTo(AutomationControlType.Separator),
                    "a toolbar rule carries structure and must publish it");
            }
        });
    }

    /// <summary>And it must still be DRAWABLE: a rule that publishes itself perfectly while carrying no ink or no
    /// size would satisfy every assertion above and show the user nothing.
    /// <para>Asserted on the resolved style against the toolbar the rule stands on, not on a captured frame. That
    /// is a deliberate limit, and this surface is where its cost is clearest: a setter the renderer ignores passes
    /// here (F-1's <c>Opacity</c> did exactly that), and so does a third-party control that drops the value on one
    /// OS — Svg.Controls.Skia.Avalonia ≤12.0.0.13 ignored <c>CurrentColor</c> on Linux while the property read back
    /// correctly, which no property assertion in this suite could see (2026-08-17, see the package pin in
    /// Directory.Packages.props). A plain <see cref="Separator"/> renders through the standard pipeline rather than
    /// a custom draw operation, which is what makes the trade acceptable HERE and not everywhere.</para></summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheRule_CarriesInkAndSizeAgainstTheBarBehindIt()
    {
        MainWindow window = await ShowShellAsync();

        Control rule = Rules(window).Single();
        IBrush? ink = (rule as TemplatedControl)?.Background;
        IBrush? bar = window.GetVisualDescendants().OfType<Border>()
            .Single(b => AutomationProperties.GetAutomationId(b) == "Toolbar").Background;

        Assert.Multiple(() =>
        {
            Assert.That(ink, Is.Not.Null, "the rule has a fill to draw with");
            Assert.That(ink?.ToString(), Is.Not.EqualTo(Brushes.Transparent.ToString()),
                "and that fill is not nothing");
            Assert.That(ink?.ToString(), Is.Not.EqualTo(bar?.ToString()),
                "and differs from the toolbar behind it, or the line is invisible against its own ground");
            Assert.That(rule.Bounds.Width, Is.GreaterThan(0), "the rule was laid out with a real width");
            Assert.That(rule.Bounds.Height, Is.GreaterThan(0), "the rule was laid out with a real height");
            Assert.That(rule.IsEffectivelyVisible, Is.True, "and nothing above it hid the whole rule");
        });
    }

    /// <summary>The bar's automation ids in visual order, with each rule rendered as "|" — the shape a toolbar
    /// dump reads, so the expectation above is written the way the vendor's own dump is.</summary>
    private static List<string> BarLayout(MainWindow window)
    {
        Border toolbar = window.GetVisualDescendants().OfType<Border>()
            .Single(b => AutomationProperties.GetAutomationId(b) == "Toolbar");
        var layout = new List<string>();
        foreach (Control child in toolbar.GetVisualDescendants().OfType<Control>())
        {
            if (child is Button button && button.Classes.Contains("tool"))
                layout.Add(AutomationProperties.GetAutomationId(button) ?? "?");
            else if (IsRule(child))
                layout.Add("|");
        }
        return layout;
    }

    private static IReadOnlyList<Control> Rules(MainWindow window) =>
        [.. window.GetVisualDescendants().OfType<Control>().Where(IsRule)];

    // A rule is whatever the toolbar draws to separate groups — matched by its class, so the test names the ROLE
    // and not the control type it is currently implemented with. That is the whole point: the type is what changed.
    private static bool IsRule(Control control) => control.Classes.Contains("toolsep");

    private static async Task<MainWindow> ShowShellAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
