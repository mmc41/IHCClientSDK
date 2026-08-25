using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// What the panel actually SHOWS: five Danish columns over the bound findings, in severity-first order, with a
/// per-tier icon and a Danish label for every taxonomy value.
///
/// <para><b>The order assertion is the one that needs care.</b> <c>ValidateStructured</c> emits DOCUMENT-SCAN
/// order and severity is not one of its sort keys, while the panel's default is severity-first and only then scan
/// order. The two sequences therefore coincide exactly while every finding shares a severity — which is true of
/// every fixture in the corpus, <c>Project6-Errors.vis</c> included (150 Warnings, 0 Errors). A straight
/// "bound rows equal the engine's output" assertion over it would pass whether or not the sort exists at all. So
/// the fixture test asserts the SET and the verbatim messages, and the ORDER is proved separately on a
/// mixed-severity set — which, since no rule emits Info and no corpus file mixes tiers, means findings
/// constructed here.</para>
/// </summary>
public class ProblemsListTests
{
    private static ValidationFinding Finding(string code, ValidationSeverity severity, string message) =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            severity, ValidationCategory.Documentation, null, EquatableArray<FindingLocation>.Empty);

    // ── AC1: the fixture ────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task TheBoundRowsAreExactlyTheEnginesFindingsWithTheirMessagesVerbatim()
    {
        using ProblemsShellRig rig = new();
        await rig.Harness.Session.OpenAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));
        await rig.SettleAsync();

        EquatableArray<ValidationFinding> expected =
            rig.Harness.ProjectService.ValidateStructured(rig.Harness.Session.Current!);

        Assert.Multiple(() =>
        {
            Assert.That(expected, Is.Not.Empty, "sanity: the fixture must produce findings, or this gate is vacuous");
            Assert.That(rig.Panel.Rows.Count, Is.EqualTo(expected.Length), "every finding gets a row, none is dropped");
            Assert.That(rig.Panel.Rows.Select(r => r.Code).OrderBy(c => c),
                Is.EqualTo(expected.Select(f => f.Code.Value).OrderBy(c => c)),
                "the same SET of codes — the panel neither filters nor invents");
            Assert.That(rig.Panel.Rows.Select(r => r.Message).OrderBy(m => m, StringComparer.Ordinal),
                Is.EqualTo(expected.Select(f => f.Problem.Message).OrderBy(m => m, StringComparer.Ordinal)),
                "messages byte-verbatim: a presentation path never re-derives or re-words a bound sentence");
        });
    }

    // ── R5: the default order ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task TheDefaultOrderIsSeverityFirstAndOnlyThenTheEnginesScanOrder()
    {
        // Deliberately NOT in severity order as the engine emitted it, and deliberately with two findings per
        // tier, so the assertion can tell "sorted by severity" from "left as-is" AND check the tie-break.
        // Asserted over the PANEL's own bound rows rather than a stand-in ordering helper: the contract is what
        // the list shows, and a second implementation of it would let the real sort drift while staying green.
        using ProblemsRig rig = new(
            ProblemsTestData.Finding(ValidationSeverity.Warning, "w1", "Advarsel et."),
            ProblemsTestData.Finding(ValidationSeverity.Info, "i1", "Oplysning et."),
            ProblemsTestData.Finding(ValidationSeverity.Error, "e1", "Fejl et."),
            ProblemsTestData.Finding(ValidationSeverity.Warning, "w2", "Advarsel to."),
            ProblemsTestData.Finding(ValidationSeverity.Info, "i2", "Oplysning to."),
            ProblemsTestData.Finding(ValidationSeverity.Error, "e2", "Fejl to."));
        await rig.WithNewProjectAsync();

        Assert.That(rig.Panel.Rows.Select(r => r.Code), Is.EqualTo(new[] { "e1", "e2", "w1", "w2", "i1", "i2" }),
            "Errors, then Warnings, then Info — and WITHIN a tier the engine's own scan order, untouched. "
            + "A sort that reordered within a tier would scramble the document walk a reader navigates by.");
    }

    [Test]
    public async Task TheBoundRowsComeOutInThatSameOrder()
    {
        using ProblemsShellRig rig = new();
        await rig.Harness.Session.NewAsync();
        await rig.SettleAsync();

        Assert.That(rig.Panel.Rows.Select(r => r.Severity),
            Is.Ordered.Using<ValidationSeverity>((a, b) => ((int)a).CompareTo((int)b)),
            "the enum's own order IS the severity order — Error 0, Warning 1, Info 2");
    }

    // ── A7: the Danish surface ──────────────────────────────────────────────────────────────────────────────

    [Test]
    public void EachSeverityTierHasItsDanishLabel()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProblemsPanelViewModel.SeverityLabel(ValidationSeverity.Error), Is.EqualTo("Fejl"));
            Assert.That(ProblemsPanelViewModel.SeverityLabel(ValidationSeverity.Warning), Is.EqualTo("Advarsel"));
            Assert.That(ProblemsPanelViewModel.SeverityLabel(ValidationSeverity.Info), Is.EqualTo("Information"));
        });
    }

    /// <summary>
    /// The taxonomy needs Danish too, and it has no Danish anywhere in the SDK: the eight categories are English
    /// member names with a three-letter short code beside them, and neither belongs on a Danish screen. Choosing
    /// the label here is presentation of a TAXONOMY value, not of message text, so it does not breach the
    /// render-messages-whole rule — a category is a filter key the panel names, not a sentence the catalogue wrote.
    /// </summary>
    [Test]
    public void EveryCategoryRendersADanishLabelAndNoneIsLeftAsItsEnglishMemberName()
    {
        ValidationCategory[] all = Enum.GetValues<ValidationCategory>();

        Assert.Multiple(() =>
        {
            foreach (ValidationCategory category in all)
            {
                string label = ProblemsPanelViewModel.CategoryLabel(category);
                Assert.That(label, Is.Not.Empty, $"{category} has a label");
                Assert.That(label, Is.Not.EqualTo(category.ToString()),
                    $"{category} must not fall through to its English member name");
            }

            Assert.That(all.Select(ProblemsPanelViewModel.CategoryLabel).Distinct().Count(), Is.EqualTo(all.Length),
                "eight distinct labels — two categories sharing one would make the Kategori column a worse filter "
                + "than the enum it renders");
        });
    }

    // ── R8: the icons ───────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void EachTierWiresItsOwnAssetAndTheRefusalGlyphIsNotOneOfThem()
    {
        string[] wired =
        [
            ProblemsPanelViewModel.SeverityIcon(ValidationSeverity.Error),
            ProblemsPanelViewModel.SeverityIcon(ValidationSeverity.Warning),
            ProblemsPanelViewModel.SeverityIcon(ValidationSeverity.Info),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(wired, Is.EqualTo(new[]
            {
                "/Assets/severity-error.svg", "/Assets/severity-warning.svg", "/Assets/severity-info.svg",
            }));
            Assert.That(wired.Any(path => path.Contains("fatal", StringComparison.OrdinalIgnoreCase)), Is.False,
                "severity-fatal.svg is a REFUSAL disposition glyph. A refusal is not a finding, so it has no tier "
                + "here and must never appear in the panel");
        });
    }

    // ── The realized table ──────────────────────────────────────────────────────────────────────────────────

    [AvaloniaTest]
    public async Task TheTableCarriesTheFiveDanishColumnsInSpecOrder()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TableView table = window.GetLogicalDescendants().OfType<TableView>().Single();

        Assert.That(table.Columns.Select(c => c.Header?.ToString()),
            Is.EqualTo(new[] { "Alvor", "Kode", "Besked", "Element", "Kategori" }));

        window.Close();
    }

    [AvaloniaTest]
    public async Task TheTableIsAddressableAndVirtualizesSoALongListStaysAffordable()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TableView table = window.GetLogicalDescendants().OfType<TableView>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(AutomationProperties.GetAutomationId(table), Is.EqualTo("ProblemsList"));
            Assert.That(table.GetVisualDescendants().OfType<VirtualizingStackPanel>(), Is.Not.Empty,
                "the fixture corpus already reaches 150 rows on one project; realizing every row is the "
                + "difference between a panel that opens and one that stalls");
        });

        window.Close();
    }

    /// <summary>
    /// A row a driver — or a screen reader — can actually read. The row containers are realized from an
    /// ItemsSource, so they are exempt from the shell's authored-control automation audit; that exemption is
    /// about where the id COMES FROM, not about whether one exists, which is what this asserts.
    /// </summary>
    [AvaloniaTest]
    public async Task ARealizedRowIsReadableOverAutomation()
    {
        // A shell on a FAKE clock that is never advanced: no quiet period elapses, so no run completes and the
        // list stays empty. That matters here — the panel virtualizes, so a row appended after ~30 real findings
        // is simply never realized, and the test would be asserting about a container that does not exist.
        using ProblemsShellRig rig = new();
        await rig.Harness.Session.NewAsync();
        Window window = new MainWindow { DataContext = rig.Shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.That(rig.Panel.Rows, Is.Empty, "precondition: nothing has validated yet");

        ProblemRowViewModel mine = new(
            ValidationSeverity.Warning, "doc-name-empty", "Navnet mangler.",
            ValidationCategory.Documentation, null, "utcs_project");
        rig.Panel.Rows.Add(mine);
        Dispatcher.UIThread.RunJobs();

        TableViewRow row = window.GetVisualDescendants().OfType<TableViewRow>()
            .Single(r => ReferenceEquals(r.DataContext, mine));

        Assert.Multiple(() =>
        {
            Assert.That(AutomationProperties.GetAutomationId(row), Is.EqualTo("doc-name-empty"),
                "the row publishes its finding's code, the same way a tree row publishes its node kind");
            Assert.That(AutomationProperties.GetName(row), Does.Contain("Navnet mangler."),
                "and its accessible name carries the sentence a reader needs, not just a code");
        });

        window.Close();
    }
}
