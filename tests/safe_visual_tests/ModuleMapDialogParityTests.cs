using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Ihc.Vis;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The data-line modules dialog must present what IHC Visual's <c>Datalinie moduler</c> presents. Measured
/// against the vendor's dialog on <c>project5-Dokumentation.vis</c>: two captioned group boxes —
/// <c>Indgangsmoduler</c> then <c>Udgangsmoduler</c> — each a four-column grid headed
/// <c>Datalinie · Modul type · Lokalitet · Beskrivelse</c>, listing EVERY data line (8 input, 16 output) with
/// undocumented lines shown as not in use, and a single OK button.
///
/// OpenVisual instead showed the addressed-terminal occupancy map (address / product / terminal) over only the
/// lines in use, which is a different fact about the installation entirely.
/// </summary>
public class ModuleMapDialogParityTests
{
    private static ModuleMapWindow Window() =>
        new() { DataContext = DatalineModuleMap.Empty };

    // The logical tree, not the visual one: an unshown window has applied no templates.
    private static HeaderedContentControl[] Groups(Window window) =>
        window.GetLogicalDescendants().OfType<HeaderedContentControl>().ToArray();

    private static string[] TextsIn(HeaderedContentControl group) =>
        group.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text!).ToArray();

    /// <summary>Two captioned groups, inputs before outputs, as the vendor stacks them.</summary>
    [AvaloniaTest]
    public void Dialog_PresentsTheInputAndOutputModuleGroups()
    {
        Assert.That(Groups(Window()).Select(g => g.Header),
            Is.EqualTo(new[] { "Indgangsmoduler", "Udgangsmoduler" }));
    }

    /// <summary>Each group is headed by the vendor's four columns, in the vendor's order. These are the same
    /// four the installation report's module tables use, so the dialog and the report agree.</summary>
    [AvaloniaTest]
    public void EachGroup_CarriesTheVendorsFourColumnHeaders()
    {
        string[] expected = ["Datalinie", "Modul type", "Lokalitet", "Beskrivelse"];

        Assert.Multiple(() =>
        {
            foreach (HeaderedContentControl group in Groups(Window()))
            {
                Assert.That(TextsIn(group).Take(4), Is.EqualTo(expected), $"headers of {group.Header}");
            }
        });
    }

    /// <summary>Every data line gets a row, not just the documented ones — the vendor lists all 8 input and all
    /// 16 output lines so a reader sees which are still free.</summary>
    [AvaloniaTest]
    public void Dialog_ListsEveryDataLine_IncludingTheUndocumentedOnes()
    {
        var lists = Window().GetLogicalDescendants().OfType<ItemsControl>().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(lists, Has.Length.EqualTo(2));
            Assert.That(((IEnumerable)lists[0].ItemsSource!).Cast<object>().Count(), Is.EqualTo(8));
            Assert.That(((IEnumerable)lists[1].ItemsSource!).Cast<object>().Count(), Is.EqualTo(16));
        });
    }

    /// <summary>The rows are presentation only: no selectable list, so the read-only grid offers no affordance
    /// suggesting a row can be picked or edited.</summary>
    [AvaloniaTest]
    public void Dialog_RowsCarryNoSelectionAffordance()
    {
        Assert.That(Window().GetLogicalDescendants().OfType<ListBox>(), Is.Empty);
    }

    /// <summary>The output group is taller than the dialog — 16 lines do not fit — so it MUST scroll. Measured
    /// live on <c>g10 4-10-2025</c> it did not: output lines 13–16 were clipped at the group's lower edge and no
    /// gesture reached them, because the group-box template wrapped its content in a <c>StackPanel</c>, which
    /// measures its child with infinite height. Given infinite height a ScrollViewer never overflows, so it
    /// reported viewport == extent (nothing to scroll) and the window clipped the overflow instead. The vendor's
    /// SysListView32 scrolls to line 16, and "every data line is listed" is only true if a reader can reach them.
    /// <para>
    /// Asserted on a laid-out window, not on markup: whether the list overflows is a measure-pass fact, and the
    /// defect was invisible in the tree (all 16 rows were present as elements the whole time).
    /// </para>
    /// </summary>
    [AvaloniaTest]
    public void OutputGroup_ActuallyScrolls_SoLines13To16AreReachable()
    {
        Window window = ShowLaidOut();
        try
        {
            ScrollViewer[] scrollers = window.GetLogicalDescendants().OfType<ScrollViewer>().ToArray();

            Assert.That(scrollers, Has.Length.EqualTo(2), "one scroller per module group");
            ScrollViewer outputs = scrollers[1];

            Assert.Multiple(() =>
            {
                Assert.That(outputs.Viewport.Height, Is.GreaterThan(0), "the output list was laid out");
                Assert.That(outputs.Extent.Height, Is.GreaterThan(outputs.Viewport.Height),
                    "16 output lines do not fit the dialog, so the list must overflow — an extent equal to the "
                    + "viewport means the content was measured unconstrained and the rows past the fold are "
                    + "clipped rather than scrollable");
                Assert.That(outputs.Viewport.Height, Is.LessThanOrEqualTo(window.Height),
                    "…and the viewport is bounded by the dialog, not by its own content");
            });
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Standing scrollbars, as the vendor's list controls carry: Avalonia's default overlay scrollbar
    /// appears only on hover, which renders a list that continues below the fold exactly like one that ends
    /// there.</summary>
    [AvaloniaTest]
    public void Dialog_KeepsItsScrollbarsStanding_AsTheVendorsListsDo()
    {
        var scrollers = Window().GetLogicalDescendants().OfType<ScrollViewer>().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(scrollers, Has.Length.EqualTo(2), "one scroller per module group");
            Assert.That(scrollers.Select(s => s.AllowAutoHide), Is.All.False,
                "the scrollbar stays visible rather than hiding until hovered");
        });
    }

    /// <summary>A shown, measured, arranged window — the layout facts above are only true of a window that has
    /// been through a real layout pass. The empty map is enough: it already lists all 8 + 16 data lines, which is
    /// the whole point of the view (undocumented lines are listed too), so the overflow does not depend on any
    /// project's contents.</summary>
    private static Window ShowLaidOut()
    {
        Window window = Window();
        window.Show();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
        return window;
    }

    /// <summary>The vendor's dialog commits nothing — it has a single OK button and no Cancel.</summary>
    [AvaloniaTest]
    public void Dialog_HasASingleDismissButton()
    {
        var buttons = Window().GetLogicalDescendants().OfType<Button>().ToArray();

        Assert.That(buttons.Select(b => b.Content), Is.EqualTo(new[] { "OK" }));
    }
}
