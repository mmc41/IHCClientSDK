using System.Collections;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
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

    /// <summary>The vendor's dialog commits nothing — it has a single OK button and no Cancel.</summary>
    [AvaloniaTest]
    public void Dialog_HasASingleDismissButton()
    {
        var buttons = Window().GetLogicalDescendants().OfType<Button>().ToArray();

        Assert.That(buttons.Select(b => b.Content), Is.EqualTo(new[] { "OK" }));
    }
}
