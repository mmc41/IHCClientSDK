using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The export control on the Problemer heading: when it is offered, and what it hands over.
///
/// <para><b>The gate is a CORRECTNESS gate, not a UX one.</b> The two states it excludes are exactly the two in
/// which the written file's header would contradict its body — <c>Validating</c>, where nothing is bound so an
/// empty file would read as a clean bill of health, and <c>Stale</c>, where the findings describe a superseded
/// tree while the file's source and save stamp would name the current one. Everything else is exportable,
/// including a clean project and including a panel with every tier switched off.</para>
///
/// <para><b>What it hands over is the panel's own list.</b> <c>Rows</c> is already filtered by the tier toggles
/// and ordered by the chosen column, so one projection satisfies both fidelity requirements at once. The tests
/// below therefore assert against <c>Rows</c> rather than against the validation outcome — index-aligning the
/// two is exactly the mistake this design exists to prevent.</para>
/// </summary>
public class ProblemsExportCommandTests
{
    private static ValidationFinding Finding(
        string code, ValidationSeverity severity, string message = "Besked") =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            severity, ValidationCategory.Documentation,
            new FindingLocation("utcs_project", null, null), EquatableArray<FindingLocation>.Empty);

    // ── The four states ─────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task TheCommandIsOfferedWhenThereAreFindings()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Findings), "precondition");
            Assert.That(rig.Panel.CanExport, Is.True);
            Assert.That(rig.Panel.ExportCommand.CanExecute(null), Is.True);
        });
    }

    /// <summary>
    /// A clean project is exportable. A file saying "this save, these tiers, nothing found" is a legitimate
    /// record and is the same statement the panel is already making on screen.
    /// </summary>
    [Test]
    public async Task TheCommandIsOfferedWhenTheProjectIsClean()
    {
        using ProblemsRig rig = new();
        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Clean), "precondition");
            Assert.That(rig.Panel.CanExport, Is.True);
        });
    }

    /// <summary>Nothing is bound yet, so there is no list — and an empty file would read as a clean project.</summary>
    [Test]
    public void TheCommandIsWithheldWhileValidating()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Validating), "precondition");
            Assert.That(rig.Panel.CanExport, Is.False);
            Assert.That(rig.Panel.ExportCommand.CanExecute(null), Is.False);
        });
    }

    /// <summary>
    /// The findings on screen are about a tree the document has moved past, while the file would name the
    /// current save. It would be internally inconsistent and would say so nowhere.
    /// </summary>
    [Test]
    public async Task TheCommandIsWithheldWhileTheResultIsStale()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();
        Assert.That(rig.Panel.CanExport, Is.True, "precondition: it was offered before the edit");

        await rig.Harness.Session.NewAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Validating).Or.EqualTo(ProblemsState.Stale));
            Assert.That(rig.Panel.CanExport, Is.False, "a moved document withdraws the export until it settles");
        });
    }

    /// <summary>
    /// Every tier switched off still exports. That writes an empty list which RECORDS that it included no
    /// tiers — a different file from the clean project's, and the case that makes the severities record
    /// load-bearing rather than merely principled.
    /// </summary>
    [Test]
    public async Task TheCommandIsStillOfferedWithEveryTierSwitchedOff()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();

        foreach (ProblemsTierViewModel tier in rig.Panel.Tiers)
        {
            tier.IsShown = false;
        }

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Is.Empty, "precondition: the list really is empty");
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Findings),
                "the state is about the RESULT, not about what survived the filters");
            Assert.That(rig.Panel.CanExport, Is.True);
        });
    }

    // ── Why it is withheld ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A withheld export SAYS why, in the same <c>Availability</c> shape the registry hands the menu bar and the
    /// toolbar. The two refused states carry DIFFERENT sentences on purpose: "nothing has been validated yet" and
    /// "what you are looking at is out of date" are different things to wait for, and one shared sentence would
    /// be wrong about one of them.
    /// </summary>
    [Test]
    public async Task TheWithheldExportSaysWhy()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));

        Availability validating = rig.Panel.ExportAvailability;
        await rig.WithNewProjectAsync();
        Availability offered = rig.Panel.ExportAvailability;
        await rig.Harness.Session.NewAsync();
        Availability moved = rig.Panel.ExportAvailability;

        Assert.Multiple(() =>
        {
            Assert.That(validating.Enabled, Is.False);
            Assert.That(validating.Reason, Is.EqualTo(ProblemsPanelViewModel.ExportWhileValidatingReason));
            Assert.That(offered, Is.EqualTo(Availability.Allow), "an offered command carries no reason");
            Assert.That(moved.Enabled, Is.False);
            Assert.That(moved.Reason, Is.Not.Null.And.Not.Empty, "the moved document explains itself too");
        });
    }

    /// <summary>The stale sentence specifically — reached by editing past a bound result rather than by
    /// constructing the state, so it is the sentence a user actually meets.</summary>
    [Test]
    public async Task TheStaleExportNamesTheEdit()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();
        await rig.Harness.Session.AddLocalityAsync();

        Assert.That(
            rig.Panel.State is ProblemsState.Stale ? rig.Panel.ExportAvailability.Reason : null,
            Is.EqualTo(ProblemsPanelViewModel.ExportWhileStaleReason).Or.Null,
            "a stale panel's reason is the edit one; a panel that already re-bound has no reason at all");
    }

    /// <summary>
    /// The control announces ONE text, and it is the alternative of the two: what the file is while the export
    /// can be written, why it cannot while it cannot. That is what makes a single tooltip and a single HelpText
    /// binding correct rather than a lossy simplification.
    /// </summary>
    [Test]
    public async Task TheHintIsTheHelpTextWhileAvailableAndTheReasonWhenNot()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));

        Assert.That(rig.Panel.ExportHint, Is.EqualTo(ProblemsPanelViewModel.ExportWhileValidatingReason),
            "withheld: the hint is the reason");

        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.ExportHint, Is.EqualTo(ProblemsPanelViewModel.ExportHelpText),
                "offered: the hint is what the file is");
            Assert.That(ProblemsPanelViewModel.ExportHelpText, Is.EqualTo("Gem panelets liste som en XML-fil"));
        });
    }

    // ── Notification ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The command's availability is bound, so it has to be told when the state moves — otherwise the button
    /// stays greyed out after a validation completes and the user has to click elsewhere to wake it.
    /// </summary>
    [Test]
    public async Task CompletingAValidationNotifiesTheCommandsAvailability()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        int notified = 0;
        rig.Panel.ExportCommand.CanExecuteChanged += (_, _) => notified++;
        int availability = 0;
        rig.Panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ProblemsPanelViewModel.ExportAvailability)
                or nameof(ProblemsPanelViewModel.ExportHint))
                availability++;
        };

        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Findings), "precondition");
            Assert.That(notified, Is.GreaterThan(0));
            // The BOUND half of the same move: without this the button stays greyed under its stale sentence
            // after the run completes, which is the same defect as the command not re-querying, one layer up.
            Assert.That(availability, Is.GreaterThan(0), "the button's grey and its hint were told as well");
        });
    }

    /// <summary>
    /// A tier toggle does NOT notify, and that is correct rather than an omission: under this gate hiding a
    /// tier changes what the file contains, never whether it can be written. Notifying anyway would be
    /// harmless noise, but asserting the absence is what keeps the gate's reasoning honest — the day it starts
    /// notifying is the day something has started depending on the rows.
    /// </summary>
    [Test]
    public async Task ATierToggleDoesNotNotifyTheCommandsAvailability()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();

        int notified = 0;
        rig.Panel.ExportCommand.CanExecuteChanged += (_, _) => notified++;
        int availability = 0;
        rig.Panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ProblemsPanelViewModel.ExportAvailability)
                or nameof(ProblemsPanelViewModel.ExportHint))
                availability++;
        };
        rig.Panel.Errors.IsShown = false;

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Is.Empty, "precondition: the toggle really did move the rows");
            Assert.That(notified, Is.Zero);
            Assert.That(availability, Is.Zero, "…and neither does the button's grey or its hint");
            Assert.That(rig.Panel.CanExport, Is.True, "and it is still offered");
        });
    }

    // ── What the request carries ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The request holds the VISIBLE rows' findings, filtered and in the order shown — asserted against
    /// <c>Rows</c> by reference, never against the validation outcome, which is neither filtered nor sorted.
    /// </summary>
    [Test]
    public async Task TheRequestCarriesTheVisibleRowsFindingsInTheVisibleOrder()
    {
        using ProblemsRig rig = new(
            Finding("c-code", ValidationSeverity.Error),
            Finding("a-code", ValidationSeverity.Warning),
            Finding("b-code", ValidationSeverity.Info));
        await rig.WithNewProjectAsync();

        rig.Panel.Tiers.Single(t => t.Severity == ValidationSeverity.Info).IsShown = false;
        rig.Panel.Columns.Single(c => c.Column == ProblemsColumn.Code).SortCommand.Execute(null);
        await rig.Panel.ExportCommand.ExecuteAsync(null);

        FindingsExportRequest request = rig.Exported.Single();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Has.Count.EqualTo(2), "precondition: the filter dropped one");
            Assert.That(
                request.Findings.ToArray(),
                Is.EqualTo(rig.Panel.Rows.OfType<ProblemRowViewModel>().Select(r => r.Finding).ToArray()),
                "the visible rows' own findings, in their order");
            Assert.That(
                request.Findings.Select(f => f.Code.Value), Is.EqualTo(new[] { "a-code", "c-code" }),
                "sorted by code, and without the hidden Info row");
        });
    }

    /// <summary>
    /// A findings file is a statement about the PROJECT, so the faults listed beside the findings never reach it
    /// (D05). A panel showing nothing BUT faults therefore writes a file with no finding in it at all — which is
    /// the honest answer rather than an empty one: the project really does have no findings.
    /// </summary>
    /// <remarks>
    /// Asserted on the WRITTEN FILE, not only on the request. The request carrying no fault is the panel doing
    /// its job; a file with no <c>finding</c> element is what a support case actually receives, and only the
    /// second of those is the promise. The file is written through the same facade call the export workflow
    /// makes, so nothing between the panel and the bytes is stubbed out.
    /// </remarks>
    [Test]
    public async Task APanelShowingOnlyFaultsExportsAFileWithNoFindingElement()
    {
        using ProblemsRig rig = new();
        await rig.WithNewProjectAsync();
        rig.InternalErrors.Append(ProblemsTestData.Fault());

        await rig.Panel.ExportCommand.ExecuteAsync(null);

        FindingsExportRequest request = rig.Exported.Single();
        string path = Path.Combine(rig.Harness.TempDir, "faults-only.xml");
        await rig.Harness.ProjectService.ExportFindings(
            rig.Harness.Session.Current!, request.Findings.AsImmutableArray(), path,
            FindingExportOptions.Default);
        string written = await File.ReadAllTextAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Has.Count.EqualTo(1),
                "non-vacuity: the panel really is listing the fault");
            Assert.That(request.Findings, Is.Empty, "no fault is handed over as if it were a finding");
            Assert.That(written, Does.Contain("ihc_project_findings"),
                "non-vacuity: a real export document really was written");
            Assert.That(written, Does.Not.Contain("<finding"),
                "and the file a support case receives holds no finding element at all");
            Assert.That(written, Does.Not.Contain("internal.rule-failed"),
                "nor the fault's code anywhere else in it");
        });
    }

    /// <summary>The order label names the sorted column, and marks a descending sort.</summary>
    [Test]
    public async Task TheRequestLabelsTheOrderItIsIn()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();
        ProblemsColumnViewModel code = rig.Panel.Columns.Single(c => c.Column == ProblemsColumn.Code);

        code.SortCommand.Execute(null);
        await rig.Panel.ExportCommand.ExecuteAsync(null);
        code.SortCommand.Execute(null);
        await rig.Panel.ExportCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Exported[0].Order, Is.EqualTo("host:code"));
            Assert.That(rig.Exported[1].Order, Is.EqualTo("host:code desc"), "the second click reversed it");
        });
    }

    /// <summary>
    /// The shown tiers, in ENUM order — not the order they were clicked. Two users who hid the same tiers in a
    /// different sequence must produce the same file.
    /// <para>
    /// The set is DEDUPLICATED, which the four-tier panel forced: Fatale fejl and Fejl are both
    /// <see cref="ValidationSeverity.Error"/>, so a file would otherwise name Error twice. It also means this
    /// attribute alone cannot say which of those two tiers was shown — the reason the export records its
    /// fatal filter separately.
    /// </para>
    /// </summary>
    [Test]
    public async Task TheRequestNamesTheShownSeveritiesInEnumOrder()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();

        // Off then on again, in a deliberately scrambled sequence, so click order and enum order differ.
        rig.Panel.Tiers.Single(t => t.Tier == ProblemsTier.Error).IsShown = false;
        rig.Panel.Tiers.Single(t => t.Tier == ProblemsTier.Info).IsShown = false;
        rig.Panel.Tiers.Single(t => t.Tier == ProblemsTier.Error).IsShown = true;

        await rig.Panel.ExportCommand.ExecuteAsync(null);

        Assert.That(
            rig.Exported.Single().Severities.ToArray(),
            Is.EqualTo(new[] { ValidationSeverity.Error, ValidationSeverity.Warning }));
    }

    /// <summary>Every tier off is recorded as an empty set, which is what stops that file reading as clean.</summary>
    [Test]
    public async Task EveryTierOffIsRecordedAsNoSeveritiesAtAll()
    {
        using ProblemsRig rig = new(Finding("a", ValidationSeverity.Error));
        await rig.WithNewProjectAsync();

        foreach (ProblemsTierViewModel tier in rig.Panel.Tiers)
        {
            tier.IsShown = false;
        }

        await rig.Panel.ExportCommand.ExecuteAsync(null);
        FindingsExportRequest request = rig.Exported.Single();

        Assert.Multiple(() =>
        {
            Assert.That(request.Findings, Is.Empty);
            Assert.That(request.Severities, Is.Empty);
        });
    }

    // ── Where the control sits ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The button is on the heading and to the RIGHT of the tier toggles. Asserted on rendered bounds, because
    /// the failure this guards against is a layout one: a horizontal <c>StackPanel</c> packs its children left
    /// and ignores alignment, so a button that "looks right" in the markup renders next to the Info toggle.
    /// </summary>
    /// <remarks>
    /// The two edges are translated into WINDOW space rather than read off <c>Bounds</c>, which is
    /// parent-relative: the button now sits inside the tooltip-carrying wrapper (its own coordinate origin), so
    /// a raw <c>Bounds.X</c> reads ~0 and the comparison would answer a question about the wrapper's interior.
    /// </remarks>
    [AvaloniaTest]
    public async Task TheExportButtonSitsOnTheHeadingRightOfTheTierToggles()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        Window window = new MainWindow { DataContext = rig.Shell };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Button export = window.GetVisualDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetAutomationId(b) == ProblemsPanelViewModel.ExportAutomationId);
        Control infos = window.GetVisualDescendants().OfType<Control>()
            .First(c => AutomationProperties.GetAutomationId(c) == rig.Panel.Infos.AutomationId);

        double exportLeft = InWindow(export, window).X;
        double infosRight = InWindow(infos, window).X + infos.Bounds.Width;
        TextBlock title = window.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Name == "ProblemsPanelHeader");
        Border headingBand = title.FindAncestorOfType<Border>()!;

        Assert.Multiple(() =>
        {
            Assert.That(exportLeft, Is.GreaterThan(infosRight), "right of every tier toggle");
            Assert.That(
                export.GetVisualAncestors(), Does.Contain(headingBand),
                "and in the same heading band as the title, not in the row area");
            Assert.That(
                AutomationProperties.GetName(export), Is.EqualTo(ProblemsPanelViewModel.ExportAccessibleName));
            Assert.That(
                AutomationProperties.GetHelpText(export), Is.EqualTo(rig.Panel.ExportHint),
                "the announced text follows the panel's own hint, which is the reason while it is withheld");
        });

        window.Close();
    }

    /// <summary>
    /// The withheld button really is greyed, and the sentence saying why really is reachable — on the WRAPPER,
    /// because a disabled control shows no tooltip of its own. Both halves are asserted on the rendered tree,
    /// since a view-model that computes the right values and markup that binds neither is the failure here.
    /// </summary>
    [AvaloniaTest]
    public async Task TheWithheldExportButtonIsGreyedAndItsWrapperCarriesTheReason()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        Window window = new MainWindow { DataContext = rig.Shell };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Button export = window.GetVisualDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetAutomationId(b) == ProblemsPanelViewModel.ExportAutomationId);
        Border wrapper = export.FindAncestorOfType<Border>()!;

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.CanExport, Is.False, "precondition: nothing is bound yet");
            Assert.That(export.IsEffectivelyEnabled, Is.False, "the button is greyed");
            Assert.That(ToolTip.GetTip(wrapper), Is.EqualTo(rig.Panel.ExportHint),
                "and the wrapper — which a disabled child does pass its pointer to — carries the reason");
            Assert.That(wrapper.Padding, Is.EqualTo(default(Thickness)),
                "the wrapper adds no inset: the heading band's height is measured elsewhere");
        });

        window.Close();
    }

    // Where a control's top-left sits in the WINDOW's coordinates, so two controls in different parents can be
    // compared at all. Falls back to the raw bounds only if the visual is not connected, which would fail the
    // assertion that used it rather than silently passing.
    private static Point InWindow(Control control, Window window) =>
        control.TranslatePoint(default, window) ?? control.Bounds.Position;

    /// <summary>
    /// The button inherits the panel's own mono face and size. A default-styled button is set in the app font
    /// at the workspace size, which makes the heading band taller — the property the density tests measure,
    /// and a regression there would have nothing to do with the export.
    /// </summary>
    [AvaloniaTest]
    public async Task TheExportButtonWearsThePanelsOwnTypeface()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        Window window = new MainWindow { DataContext = rig.Shell };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Button export = window.GetVisualDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetAutomationId(b) == ProblemsPanelViewModel.ExportAutomationId);

        Assert.Multiple(() =>
        {
            Assert.That(export.FontFamily.Name, Is.EqualTo(ihc_openvisual.Program.MonoFontFamily));
            Assert.That(export.FontSize, Is.LessThan(window.FontSize));
        });

        window.Close();
    }
}
