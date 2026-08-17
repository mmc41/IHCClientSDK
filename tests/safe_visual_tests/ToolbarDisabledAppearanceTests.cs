using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Checklist dimension 18 (enabled/disabled visual semantics) for the toolbar. The reference application greys the
/// GLYPH of an unavailable toolbar button and paints no fill behind it; a box appears only under the pointer, and
/// only on a button that is actually available.
///
/// <para>Measured divergence (2026-08-10, live side by side): OpenVisual inherited Fluent's stock button treatment,
/// which does the opposite — a disabled tool button drew its glyph at FULL strength inside a filled grey box while
/// the available buttons sat flat on the toolbar. The unavailable commands were the most emphasised things on the
/// bar and read as toggled ON. Vendor evidence: fresh project, nothing selected — Klip/Kopier/Indsæt are dimmed
/// glyphs with no fill.</para>
///
/// <para>These assert the RESOLVED STYLE — the ink each glyph is told to draw in and the fill behind it — always
/// against a live enabled sibling and against the theme's own tokens rather than literal colours, so they hold in
/// every theme. What they deliberately do NOT do is read back rendered pixels, and the limit of that is worth
/// stating because it has bitten this file twice. A style assertion proves the app ASKED for the right ink; it
/// cannot prove the renderer honoured the request:</para>
/// <list type="bullet">
///   <item>An <c>Opacity</c> setter on the icon satisfied a property-level assertion while the running app drew the
///   glyph exactly as dark as before — the SVG control draws through a custom draw operation the opacity never
///   reached. That is why the ink is a COLOUR token today and not an opacity (2026-08-10).</item>
///   <item>Svg.Controls.Skia.Avalonia through 12.0.0.13 resolved the SVG <c>currentColor</c> keyword on Windows
///   only; on Linux/macOS every glyph fell back to pure black and an unavailable command rendered exactly as dark
///   as an available one, while <c>CurrentColor</c> read back correctly the whole time. Fixed by the 12.0.0.15
///   floor pinned in <c>Directory.Packages.props</c>, which carries the full measurement — a regression there is
///   invisible to this file by construction (2026-08-17).</item>
/// </list>
/// </summary>
public class ToolbarDisabledAppearanceTests : AvaloniaTestBase
{
    // Fresh project, nothing selected: the clipboard trio is gated off while New stays available. That is the
    // state the appearance rules below are measured in — and the precondition that makes them meaningful.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task FreshProject_ClipboardToolbarButtons_AreDisabledWhileNewIsEnabled()
    {
        MainWindow window = await ShowShellAsync();

        Assert.Multiple(() =>
        {
            Assert.That(ToolButton(window, "ToolbarNew").IsEffectivelyEnabled, Is.True,
                "New is always available");
            foreach (string id in new[] { "ToolbarCut", "ToolbarCopy", "ToolbarPaste" })
            {
                Assert.That(ToolButton(window, id).IsEffectivelyEnabled, Is.False,
                    $"{id} is gated off with no selection and an empty clipboard");
            }
        });
    }

    // The glyph carries the state: an unavailable command's icon takes the theme's disabled ink, an available
    // one's the ordinary ink. Asserted against the tokens themselves, so the rule survives a palette change.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DisabledToolbarButton_TakesTheDisabledIconInk()
    {
        MainWindow window = await ShowShellAsync();

        Color enabled = GlyphInk(window, "ToolbarNew");
        Color disabled = GlyphInk(window, "ToolbarCut");

        Assert.Multiple(() =>
        {
            Assert.That(enabled, Is.EqualTo(Token("IconColor")),
                "an available command's glyph draws in the ordinary icon ink");
            Assert.That(disabled, Is.EqualTo(Token("DisabledIconColor")),
                "an unavailable command's glyph draws in the disabled icon ink, as the reference application greys it");
            Assert.That(disabled, Is.Not.EqualTo(enabled),
                "and the two inks are distinguishable, which is the whole point of the pair");
        });
    }

    // And the fill must not invert the emphasis: a disabled button may not paint a background its enabled siblings
    // do not. This is the half that actually misleads — a grey box reads as "toggled on", not "off". Asserted on
    // the template part the theme's own :disabled setter lands on, which is where the stock grey box came from.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DisabledToolbarButton_PaintsNoFillAnEnabledOneLacks()
    {
        MainWindow window = await ShowShellAsync();

        IBrush? enabled = ContentFill(window, "ToolbarNew");
        IBrush? disabled = ContentFill(window, "ToolbarCut");

        Assert.Multiple(() =>
        {
            Assert.That(disabled?.ToString(), Is.EqualTo(enabled?.ToString()),
                "an unavailable toolbar button sits on the same bare toolbar as an available one");
            Assert.That(disabled?.ToString(), Is.EqualTo(Brushes.Transparent.ToString()),
                "and that shared fill is nothing at all, not a shared box");
        });
    }

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

    private static Button ToolButton(MainWindow window, string automationId) =>
        window.GetVisualDescendants().OfType<Button>()
            .Single(b => b.Classes.Contains("tool") && AutomationProperties.GetAutomationId(b) == automationId);

    /// <summary>The colour the button's glyph is told to draw itself in — the resolved end of the icon styles.</summary>
    private static Color GlyphInk(MainWindow window, string automationId) =>
        ToolButton(window, automationId).GetVisualDescendants().OfType<Avalonia.Svg.Skia.Svg>().Single()
            .CurrentColor ?? throw new AssertionException($"{automationId} has no icon ink set at all");

    /// <summary>The fill behind the glyph, read off the template part the Fluent theme's own :disabled setter
    /// targets — clearing the Button's own Background would not reach it, so that is where this must be read.</summary>
    private static IBrush? ContentFill(MainWindow window, string automationId) =>
        ToolButton(window, automationId).GetVisualDescendants().OfType<ContentPresenter>()
            .Single(p => p.Name == "PART_ContentPresenter").Background;

    private static Color Token(string key)
    {
        ThemeVariant variant = Application.Current!.ActualThemeVariant;
        Assert.That(Application.Current.TryGetResource(key, variant, out object? value), Is.True,
            $"the theme defines the {key} token");
        return (Color)value!;
    }
}
