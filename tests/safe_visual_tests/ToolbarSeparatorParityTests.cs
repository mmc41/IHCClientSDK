using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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

    /// <summary>And it must still be DRAWN. Pixels, not properties: this is the surface where an ignored setter
    /// already cost a turn (F-1's Opacity), and a rule that publishes itself perfectly while rendering nothing
    /// would satisfy every assertion above.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheRule_IsActuallyDrawn()
    {
        MainWindow window = await ShowShellAsync();
        using var frame = new ToolbarFrame(window);

        Control rule = Rules(window).Single();
        double ink = frame.DarkestInk(rule);
        double bar = frame.LuminanceLeftOf(rule);

        Assert.That(ink, Is.LessThan(bar - 8),
            $"the rule paints a visible line against the toolbar behind it (rule {ink:F0}, bar {bar:F0})");
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

    /// <summary>One rendered frame, sampled around a control. Headless renders at scale 1, so logical bounds are
    /// pixel bounds.</summary>
    private sealed class ToolbarFrame : IDisposable
    {
        private readonly WriteableBitmap _bitmap;
        private readonly ILockedFramebuffer _buffer;
        private readonly Visual _root;

        public ToolbarFrame(Window window)
        {
            _bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("the headless session rendered no frame");
            _buffer = _bitmap.Lock();
            _root = window;
        }

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

        /// <summary>The bare toolbar a few pixels to the LEFT of the rule — the ground it has to stand out from.
        /// Taken beside the rule rather than as a fixed colour so the assertion holds in either theme.</summary>
        public double LuminanceLeftOf(Visual control)
        {
            PixelRect rect = RectOf(control);
            return Luminance(Math.Max(0, rect.X - 3), rect.Y + rect.Height / 2);
        }

        private PixelRect RectOf(Visual control)
        {
            Point origin = control.TranslatePoint(default, _root)
                ?? throw new InvalidOperationException("the control is not in the window's visual tree");
            var rect = new PixelRect(
                (int)Math.Round(origin.X), (int)Math.Round(origin.Y),
                (int)Math.Round(control.Bounds.Width), (int)Math.Round(control.Bounds.Height));
            Assert.That(rect.Width, Is.GreaterThan(0), "the rule was laid out with a real width");
            Assert.That(rect.Height, Is.GreaterThan(0), "the rule was laid out with a real height");
            Assert.That(new PixelRect(_buffer.Size).Contains(rect), Is.True, "the rule is inside the frame");
            return rect;
        }

        private double Luminance(int x, int y)
        {
            long offset = (long)y * _buffer.RowBytes + (long)x * 4;
            byte first = Marshal.ReadByte(_buffer.Address, (int)offset);
            byte green = Marshal.ReadByte(_buffer.Address, (int)offset + 1);
            byte third = Marshal.ReadByte(_buffer.Address, (int)offset + 2);
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
