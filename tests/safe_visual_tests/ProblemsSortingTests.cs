using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Per-column sorting: which key each column sorts on, how the direction flips, and — the part a naive
/// implementation gets wrong on a Danish screen — how names collate.
///
/// <para><b>Danish collation is not a detail here.</b> Every element name in this application is Danish, and the
/// invariant/ordinal order a default <c>OrderBy(string)</c> gives puts Æ, Ø and Å in the middle of the alphabet
/// where a Danish reader does not look for them. In Danish they sort AFTER Z. A name column that gets this wrong
/// is not slightly off; it is a column a user cannot scan.</para>
///
/// <para><b>Two directions, and only two.</b> Ascending and descending with a visible indicator is the whole
/// specification. A third "back to the default" header state was an invention of an earlier draft — it is not
/// built, and the test below pins that a third click returns to ascending rather than to some other mode.</para>
/// </summary>
public class ProblemsSortingTests
{
    /// <summary>
    /// One finding, phrased as the row it will become. The element NAME is carried in the location's locator
    /// slot, which is exactly the fallback a finding with no parsed element uses — so these rows exercise the
    /// real bind path rather than a hand-built row list.
    /// </summary>
    private static ValidationFinding Row(
        ValidationSeverity severity, string code, string message, string elementName,
        ValidationCategory category = ValidationCategory.Documentation) =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            severity, category, new FindingLocation(elementName, null, null),
            EquatableArray<FindingLocation>.Empty);

    /// <summary>A real panel over a real workflow, bound to the given findings and nothing else.</summary>
    private static async Task<ProblemsRig> PanelWith(params ValidationFinding[] findings)
    {
        ProblemsRig rig = new(findings);
        await rig.Harness.Session.NewAsync();
        rig.Clock.Advance(ValidationWorker.DefaultDebounce);
        await rig.Panel.Idle.WaitAsync(TimeSpan.FromSeconds(10));
        return rig;
    }

    private static string[] Codes(ProblemsPanelViewModel panel) => [.. panel.Rows.Select(r => r.Code)];

    // ── The default ─────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task TheDefaultIsSeverityAscendingWhichIsWorstFirst()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "w1", "Advarsel.", "Stue"),
            Row(ValidationSeverity.Error, "e1", "Fejl.", "Køkken"),
            Row(ValidationSeverity.Info, "i1", "Oplysning.", "Bad"));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SortColumn, Is.EqualTo(ProblemsColumn.Severity));
            Assert.That(rig.Panel.SortAscending, Is.True);
            Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "e1", "w1", "i1" }),
                "ascending on severity means the enum's own order — Error, Warning, Info — which is worst first");
        });
    }

    [Test]
    public async Task SortingIsStableSoEqualKeysKeepTheEnginesDocumentScanOrder()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "first", "A.", "Stue"),
            Row(ValidationSeverity.Warning, "second", "B.", "Stue"),
            Row(ValidationSeverity.Warning, "third", "C.", "Stue"));

        rig.Panel.SortBy(ProblemsColumn.Element);

        Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "first", "second", "third" }),
            "three rows on one element must not be shuffled — document order is the ordering a reader navigates by");
    }

    // ── Direction ───────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ChoosingAColumnSortsAscendingAndChoosingItAgainReversesIt()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "b", "B.", "Bad"),
            Row(ValidationSeverity.Warning, "a", "A.", "Stue"),
            Row(ValidationSeverity.Warning, "c", "C.", "Køkken"));

        rig.Panel.SortBy(ProblemsColumn.Code);
        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SortAscending, Is.True, "a newly chosen column starts ascending");
            Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "a", "b", "c" }));
        });

        rig.Panel.SortBy(ProblemsColumn.Code);
        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SortAscending, Is.False);
            Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "c", "b", "a" }));
        });
    }

    [Test]
    public async Task AThirdChoiceReturnsToAscendingBecauseThereAreOnlyTwoDirections()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "b", "B.", "Bad"),
            Row(ValidationSeverity.Warning, "a", "A.", "Stue"));

        rig.Panel.SortBy(ProblemsColumn.Code);
        rig.Panel.SortBy(ProblemsColumn.Code);
        rig.Panel.SortBy(ProblemsColumn.Code);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SortAscending, Is.True);
            Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "a", "b" }),
                "ascending / descending / ascending — no third header state, and no hidden 'default' mode");
        });
    }

    [Test]
    public async Task SwitchingToAnotherColumnStartsThatColumnAscendingAgain()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "b", "B.", "Bad"),
            Row(ValidationSeverity.Error, "a", "A.", "Stue"));

        rig.Panel.SortBy(ProblemsColumn.Code);
        rig.Panel.SortBy(ProblemsColumn.Code);
        Assert.That(rig.Panel.SortAscending, Is.False, "precondition: descending on Kode");

        rig.Panel.SortBy(ProblemsColumn.Severity);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SortColumn, Is.EqualTo(ProblemsColumn.Severity));
            Assert.That(rig.Panel.SortAscending, Is.True, "a different column does not inherit the previous direction");
        });
    }

    // ── AC4: Danish collation ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ElementNamesCollateInDanishSoTheThreeExtraLettersSortAfterZ()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "aa", "M.", "Ålestue"),
            Row(ValidationSeverity.Warning, "ae", "M.", "Æblehave"),
            Row(ValidationSeverity.Warning, "z", "M.", "Zonestyring"),
            Row(ValidationSeverity.Warning, "oe", "M.", "Østfløj"),
            Row(ValidationSeverity.Warning, "a", "M.", "Alrum"));

        rig.Panel.SortBy(ProblemsColumn.Element);

        Assert.That(rig.Panel.Rows.Select(r => r.ElementName),
            Is.EqualTo(new[] { "Alrum", "Zonestyring", "Æblehave", "Østfløj", "Ålestue" }),
            "Æ, Ø and Å are the last three letters of the Danish alphabet. An ordinal or invariant sort scatters "
            + "them mid-alphabet, which is a name column a Danish reader cannot scan");
    }

    [Test]
    public async Task MessagesCollateInDanishToo()
    {
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "1", "Ændret adresse.", "A"),
            Row(ValidationSeverity.Warning, "2", "Zone mangler.", "B"));

        rig.Panel.SortBy(ProblemsColumn.Message);

        Assert.That(rig.Panel.Rows.Select(r => r.Message), Is.EqualTo(new[] { "Zone mangler.", "Ændret adresse." }),
            "the Besked column carries Danish sentences and collates like the element column");
    }

    [Test]
    public async Task TheCategoryColumnSortsByItsDanishLabelNotByTheEnglishEnum()
    {
        // Addressing → "Adressering" and Wiring → "Forbindelser": in the ENUM, Wiring (1) precedes Addressing (4);
        // by Danish label, Adressering precedes Forbindelser. Sorting on the enum would order the column by names
        // the user never sees.
        using ProblemsRig rig = await PanelWith(
            Row(ValidationSeverity.Warning, "w", "M.", "A", ValidationCategory.Wiring),
            Row(ValidationSeverity.Warning, "a", "M.", "B", ValidationCategory.Addressing));

        rig.Panel.SortBy(ProblemsColumn.Category);

        Assert.That(rig.Panel.Rows.Select(r => r.CategoryLabel), Is.EqualTo(new[] { "Adressering", "Forbindelser" }));
    }

    // ── The header indicator ────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task OnlyTheSortedColumnShowsADirectionArrowAndItFlipsWithTheDirection()
    {
        using ProblemsRig rig = await PanelWith(Row(ValidationSeverity.Warning, "a", "A.", "Stue"));

        rig.Panel.SortBy(ProblemsColumn.Code);
        ProblemsColumnViewModel code = rig.Panel.Columns.Single(c => c.Column == ProblemsColumn.Code);
        ProblemsColumnViewModel element = rig.Panel.Columns.Single(c => c.Column == ProblemsColumn.Element);

        Assert.Multiple(() =>
        {
            Assert.That(code.SortGlyph, Is.Not.Empty, "the sorted column says which way it is sorted");
            Assert.That(element.SortGlyph, Is.Empty, "and every other column says nothing");
        });

        string ascending = code.SortGlyph;
        rig.Panel.SortBy(ProblemsColumn.Code);

        Assert.That(code.SortGlyph, Is.Not.EqualTo(ascending), "the arrow turns over when the direction does");
    }

    [Test]
    public async Task EveryColumnIsSortableAndCarriesItsDanishTitle()
    {
        using ProblemsRig rig = await PanelWith(Row(ValidationSeverity.Warning, "a", "A.", "Stue"));

        Assert.That(rig.Panel.Columns.Select(c => c.Title),
            Is.EqualTo(new[] { "Alvor", "Kategori", "Besked", "Element", "Kode" }));
    }

    // ── The realized headers ────────────────────────────────────────────────────────────────────────────────

    [AvaloniaTest]
    public async Task EachHeaderIsAnAddressableButtonSoAKeyboardOrADriverCanSortWithoutAMouse()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The VISUAL tree, not the logical one: a column header is built inside the TableView's own
        // TableViewColumnHeadersPresenter — control-template territory — so it never appears as a logical
        // descendant of the window. (That is also why these buttons are outside the shell's authored-control
        // automation audit, which walks the logical tree; their ids are asserted here instead.)
        string[] ids =
        [
            .. window.GetVisualDescendants().OfType<Button>()
                .Select(AutomationProperties.GetAutomationId)
                .Where(id => id is not null && id.StartsWith("problems.sort.", StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)!,
        ];

        Assert.That(ids, Is.EqualTo(new[]
        {
            "problems.sort.category", "problems.sort.code", "problems.sort.element",
            "problems.sort.message", "problems.sort.severity",
        }), "one addressable sort control per column — a header a driver cannot name is a header it cannot click");

        window.Close();
    }
}
