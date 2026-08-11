using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
/// <para>These assert RENDERED PIXELS, not styled properties, and always against a live enabled sibling rather than
/// literal colours — so they hold in every theme, and they cannot be satisfied by a setter the renderer ignores.
/// That is not hypothetical: the first fix set <c>Opacity</c> on the icon, which satisfied a property-level
/// assertion while the running app rendered the glyph exactly as dark as before, because the SVG control draws
/// through a custom draw operation that the opacity never reached.</para>
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

    // The glyph carries the state: an unavailable command's icon must render visibly lighter than an available
    // one's. Measured as the darkest ink each button puts on screen — dimming raises it towards the background.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DisabledToolbarButton_RendersItsGlyphDimmed()
    {
        MainWindow window = await ShowShellAsync();
        using var frame = new RenderedFrame(window);

        double enabled = frame.DarkestInk(ToolButton(window, "ToolbarNew"));
        double disabled = frame.DarkestInk(ToolButton(window, "ToolbarCut"));

        Assert.That(disabled, Is.GreaterThan(enabled + 24),
            $"an unavailable command's glyph renders greyed, as the reference application greys it "
            + $"(darkest ink: enabled {enabled:F0}, disabled {disabled:F0} on a 0-255 scale)");
    }

    // And the fill must not invert the emphasis: a disabled button may not paint a background its enabled siblings
    // do not. This is the half that actually misleads — a grey box reads as "toggled on", not "off". Measured on
    // the button's own corner, which no glyph reaches, so any difference there is fill.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DisabledToolbarButton_PaintsNoFillAnEnabledOneLacks()
    {
        MainWindow window = await ShowShellAsync();
        using var frame = new RenderedFrame(window);

        double enabled = frame.CornerLuminance(ToolButton(window, "ToolbarNew"));
        double disabled = frame.CornerLuminance(ToolButton(window, "ToolbarCut"));

        Assert.That(disabled, Is.EqualTo(enabled).Within(4),
            $"an unavailable toolbar button sits on the same bare toolbar as an available one "
            + $"(corner luminance: enabled {enabled:F0}, disabled {disabled:F0})");
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

    /// <summary>One rendered frame of the window, sampled per control. Headless renders at scale 1, so a control's
    /// logical bounds are its pixel bounds.</summary>
    private sealed class RenderedFrame : IDisposable
    {
        private readonly WriteableBitmap _bitmap;
        private readonly ILockedFramebuffer _buffer;
        private readonly Visual _root;

        public RenderedFrame(Window window)
        {
            _bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("the headless session rendered no frame");
            _buffer = _bitmap.Lock();
            _root = window;
        }

        /// <summary>Darkest pixel the control paints — the ink of its glyph (0 = black, 255 = white).</summary>
        public double DarkestInk(Visual control)
        {
            PixelRect rect = RectOf(control);
            double darkest = 255;
            for (int y = rect.Y; y < rect.Bottom; y++)
            {
                for (int x = rect.X; x < rect.Right; x++)
                {
                    darkest = Math.Min(darkest, Luminance(x, y));
                }
            }
            return darkest;
        }

        /// <summary>The control's top-left pixel — background only, no glyph reaches it.</summary>
        public double CornerLuminance(Visual control)
        {
            PixelRect rect = RectOf(control);
            return Luminance(rect.X + 1, rect.Y + 1);
        }

        private PixelRect RectOf(Visual control)
        {
            Point origin = control.TranslatePoint(default, _root)
                ?? throw new InvalidOperationException("the control is not in the window's visual tree");
            var rect = new PixelRect(
                (int)Math.Round(origin.X), (int)Math.Round(origin.Y),
                (int)Math.Round(control.Bounds.Width), (int)Math.Round(control.Bounds.Height));
            Assert.That(rect.Width, Is.GreaterThan(0), "the control was laid out with a real size");
            Assert.That(new PixelRect(_buffer.Size).Contains(rect), Is.True,
                "the control is inside the rendered frame");
            return rect;
        }

        private double Luminance(int x, int y)
        {
            long offset = (long)y * _buffer.RowBytes + (long)x * 4;
            byte first = Marshal.ReadByte(_buffer.Address, (int)offset);
            byte green = Marshal.ReadByte(_buffer.Address, (int)offset + 1);
            byte third = Marshal.ReadByte(_buffer.Address, (int)offset + 2);
            // Both supported layouts put alpha last and the two ends of RGB at offsets 0 and 2, so green is shared
            // and only the red/blue pair swaps — which is all this has to get right.
            (double r, double b) = _buffer.Format == PixelFormat.Rgba8888 ? (first, third) : (third, first);
            return 0.299 * r + 0.587 * green + 0.114 * b;
        }

        public void Dispose()
        {
            _buffer.Dispose();
            _bitmap.Dispose();
        }
    }
}
