using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Ihc.Vis;
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
/// The four tier filter toggles — Fatal fejl, Fejl, Advarsel, Information — and the two tiers this fixture
/// is the main way to observe.
///
/// <para><b>A count is not a row count.</b> The toggles carry LIVE counts of what the project contains, so
/// hiding a tier must not change the number beside its own button — otherwise switching a tier off makes its
/// findings appear to have been fixed, which is the one thing a filter must never imply. Counts come from the
/// bound result; the list comes from the bound result minus the hidden tiers.</para>
///
/// <para><b>Two tiers are exercised synthetically, for opposite reasons.</b> No production rule emits
/// <see cref="ValidationSeverity.Info"/> today, so that tier ships empty. And a <c>Fatal fejl</c> row needs a
/// finding whose RULE refuses an operation, which the shipped corpus produces only for a handful of schema
/// guards. Findings constructed here are therefore the only way to prove either tier renders, counts, filters
/// and sorts like the other two — which is exactly why these tests build their own.</para>
/// </summary>
public class ProblemsFilterTests
{
    /// <summary>One finding of every tier — two Warnings, so a hidden tier is visibly more than one row.</summary>
    private static Task<ProblemsRig> MixedPanel() =>
        new ProblemsRig(
            ProblemsTestData.Finding(ValidationSeverity.Error, "e1", "En fejl."),
            ProblemsTestData.Finding(ValidationSeverity.Warning, "w1", "Advarsel et."),
            ProblemsTestData.Finding(ValidationSeverity.Warning, "w2", "Advarsel to."),
            ProblemsTestData.Finding(ValidationSeverity.Info, "i1", "En oplysning."))
            .WithNewProjectAsync();

    private static string[] Codes(ProblemsPanelViewModel panel) => [.. panel.Rows.Select(r => r.Code)];

    /// <summary>
    /// The panel's tier is keyed on its OWN <see cref="ProblemsTier"/>, not on the SDK's
    /// <see cref="ValidationSeverity"/>. The two were in one-to-one correspondence and so looked like one axis;
    /// they are not, and a dictionary keyed on severity cannot hold two tiers that share one — the second would
    /// overwrite the first.
    /// <para>
    /// Nothing a user or a driver can see moves with this: the shipped three keep their ids, their Danish words,
    /// their glyphs and the severity the export records. That whole tuple is pinned here, so the re-keying is
    /// held to being exactly a re-keying.
    /// </para>
    /// </summary>
    [AvaloniaTest]
    public async Task TheTiersAreKeyedOnThePanelsOwnAxisWithNothingObservableMoved()
    {
        using ProblemsRig rig = await MixedPanel();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Tiers.Select(t => t.Tier),
                Is.EqualTo(new[]
                {
                    ProblemsTier.Fatal, ProblemsTier.Error, ProblemsTier.Warning, ProblemsTier.Info,
                }).AsCollection,
                "worst first, which is also the toggle row's order");

            Assert.That(
                rig.Panel.Tiers.Select(t => (t.AutomationId, t.CountAutomationId, t.Label, t.Icon, t.Severity)),
                Is.EqualTo(new[]
                {
                    ("problems.filter.fatal", "problems.count.fatal", "Fatal fejl",
                        "/Assets/severity-fatal.svg", ValidationSeverity.Error),
                    ("problems.filter.error", "problems.count.error", "Fejl",
                        "/Assets/severity-error.svg", ValidationSeverity.Error),
                    ("problems.filter.warning", "problems.count.warning", "Advarsel",
                        "/Assets/severity-warning.svg", ValidationSeverity.Warning),
                    ("problems.filter.info", "problems.count.info", "Information",
                        "/Assets/severity-info.svg", ValidationSeverity.Info),
                }).AsCollection);
        });
    }

    /// <summary>
    /// A row reaches its tier through ONE classifier, which is what stops the tier a finding is counted under
    /// and the tier it is hidden by becoming different answers. Both the filter and the counts go through it.
    /// </summary>
    [AvaloniaTest]
    public void OneClassifierDecidesEveryFindingsTier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ProblemsPanelViewModel.TierOf(ProblemsTestData.Finding(ValidationSeverity.Error, "e", "E.")),
                Is.EqualTo(ProblemsTier.Error));
            Assert.That(
                ProblemsPanelViewModel.TierOf(ProblemsTestData.Finding(ValidationSeverity.Warning, "w", "A.")),
                Is.EqualTo(ProblemsTier.Warning));
            Assert.That(
                ProblemsPanelViewModel.TierOf(ProblemsTestData.Finding(ValidationSeverity.Info, "i", "O.")),
                Is.EqualTo(ProblemsTier.Info));
            Assert.That(
                ProblemsPanelViewModel.TierOf(ProblemsTestData.FatalFinding("f", "F.")),
                Is.EqualTo(ProblemsTier.Fatal),
                "an Error finding whose row refuses an operation is the Fatal fejl tier, and the pair is the "
                + "whole definition — neither half alone");
        });
    }

    /// <summary>One finding of every one of the four tiers, the fatal one refusing an operation.</summary>
    private static Task<ProblemsRig> FourTierPanel() =>
        new ProblemsRig(
            ProblemsTestData.FatalFinding("attr-undeclared", "Ukendt attribut."),
            ProblemsTestData.Finding(ValidationSeverity.Error, "e1", "En fejl."),
            ProblemsTestData.Finding(ValidationSeverity.Warning, "w1", "Advarsel et."),
            ProblemsTestData.Finding(ValidationSeverity.Info, "i1", "En oplysning."))
            .WithNewProjectAsync();

    /// <summary>
    /// The tier a refusing finding lands in, and — just as load-bearing — the tier it does NOT land in. Both
    /// rows are <see cref="ValidationSeverity.Error"/>, so counting them together is the failure mode, and it is
    /// the one a severity-keyed panel could not have avoided.
    /// </summary>
    [AvaloniaTest]
    public async Task ARefusingFindingIsCountedUnderFataleFejlAndNotUnderFejl()
    {
        using ProblemsRig rig = await FourTierPanel();

        Assert.Multiple(() =>
        {
            // Counting only: the Fatal tier's word, glyph and severity are pinned once, by the tuple in
            // TheTiersAreKeyedOnThePanelsOwnAxisWithNothingObservableMoved above.
            Assert.That(rig.Panel.Fatals.Count, Is.EqualTo(1), "the refusing row");
            Assert.That(rig.Panel.Errors.Count, Is.EqualTo(1), "and NOT also counted here, though it is an Error");
            Assert.That(rig.Panel.Warnings.Count, Is.EqualTo(1));
            Assert.That(rig.Panel.Infos.Count, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// The two Error tiers filter INDEPENDENTLY. Hiding either must leave the other's rows listed, which is the
    /// property a shared severity key would have destroyed — one toggle would have hidden both.
    /// </summary>
    [AvaloniaTest]
    public async Task HidingOneErrorTierLeavesTheOthersRowsListed()
    {
        using ProblemsRig rig = await FourTierPanel();

        rig.Panel.Fatals.IsShown = false;
        Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "e1", "w1", "i1" }).AsCollection,
            "the ordinary Error survives its sibling tier being hidden");

        rig.Panel.Fatals.IsShown = true;
        rig.Panel.Errors.IsShown = false;
        Assert.Multiple(() =>
        {
            Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "attr-undeclared", "w1", "i1" }).AsCollection,
                "and the fatal one survives the ordinary tier being hidden");
            Assert.That(rig.Panel.Fatals.Count, Is.EqualTo(1),
                "counts are of the result, so hiding a tier never makes another's findings look fixed");
            Assert.That(rig.Panel.Errors.Count, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// The panel states BOTH its Error filters to the export, because the severity set it also supplies cannot
    /// tell them apart: hiding *Fatale fejl* alone leaves <c>Error</c> listed exactly as before, so a file would
    /// otherwise claim to hold every error while holding only some.
    /// </summary>
    [AvaloniaTest]
    public async Task TheExportIsToldHowBothErrorFiltersStood()
    {
        using ProblemsRig rig = await FourTierPanel();

        rig.Panel.Fatals.IsShown = false;
        await rig.Panel.ExportCommand.ExecuteAsync(null);

        FindingsExportRequest request = rig.Exported.Single();

        Assert.Multiple(() =>
        {
            Assert.That(request.ErrorTiers, Is.EqualTo(new ErrorTierFilter(Refusing: false, Ordinary: true)));
            Assert.That(request.Severities, Does.Contain(ValidationSeverity.Error),
                "the severity set is UNCHANGED by hiding one of the two Error tiers — which is the whole reason "
                + "the filters have to be stated separately");
        });
    }

    /// <summary>
    /// The default sort is by TIER, worst first, so a fatal row sorts above an ordinary Error. Sorting by
    /// severity could not do it: the two carry the same value.
    /// </summary>
    [AvaloniaTest]
    public async Task FataleFejlSortsAboveFejlByDefault()
    {
        using ProblemsRig rig = await FourTierPanel();

        Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "attr-undeclared", "e1", "w1", "i1" }).AsCollection);
    }

    // ── Defaults ────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task EveryTierIsShownByDefaultBecauseAHiddenFindingIsAFindingNobodyActsOn()
    {
        using ProblemsRig rig = await MixedPanel();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Fatals.IsShown, Is.True);
            Assert.That(rig.Panel.Errors.IsShown, Is.True);
            Assert.That(rig.Panel.Warnings.IsShown, Is.True);
            Assert.That(rig.Panel.Infos.IsShown, Is.True);
            Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "e1", "w1", "w2", "i1" }));
        });
    }

    // ── Filtering ───────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task HidingATierRemovesItsRowsAndShowingItAgainBringsThemBack()
    {
        using ProblemsRig rig = await MixedPanel();

        rig.Panel.Warnings.IsShown = false;
        Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "e1", "i1" }), "both Warnings go, and nothing else does");

        rig.Panel.Warnings.IsShown = true;
        Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "e1", "w1", "w2", "i1" }),
            "session-only state, restored exactly — including the order");
    }

    [Test]
    public async Task HidingATierDoesNotChangeItsCountBecauseNothingWasFixed()
    {
        using ProblemsRig rig = await MixedPanel();

        rig.Panel.Warnings.IsShown = false;
        rig.Panel.Errors.IsShown = false;
        rig.Panel.Infos.IsShown = false;

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Is.Empty, "nothing is listed");
            Assert.That(rig.Panel.Errors.Count, Is.EqualTo(1), "but the project still has one error");
            Assert.That(rig.Panel.Warnings.Count, Is.EqualTo(2));
            Assert.That(rig.Panel.Infos.Count, Is.EqualTo(1));
            Assert.That(rig.Validation.HasBlockingFindings, Is.True,
                "and the send gate is unmoved by a filter — hiding a row is not fixing it");
        });
    }

    /// <summary>
    /// The empty-state text is about the RESULT, not about the list. With every tier switched off the list is
    /// empty and the project is not clean; saying "Ingen problemer fundet" there would tell a user their project
    /// is fine because they hid the evidence.
    /// </summary>
    [Test]
    public async Task FilteringEverythingAwayDoesNotClaimTheProjectIsClean()
    {
        using ProblemsRig rig = await MixedPanel();

        rig.Panel.Errors.IsShown = false;
        rig.Panel.Warnings.IsShown = false;
        rig.Panel.Infos.IsShown = false;

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Rows, Is.Empty);
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Findings));
            Assert.That(rig.Panel.StateText, Is.Not.EqualTo(ProblemsPanelViewModel.CleanText));
        });
    }

    [Test]
    public async Task AFilterSurvivesTheNextValidationRun()
    {
        using ProblemsRig rig = await MixedPanel();
        rig.Panel.Warnings.IsShown = false;

        await rig.Harness.Session.ApplyAsync(new Ihc.Vis.Session.AddLocality("Ny stue"));
        rig.Clock.Advance(ValidationWorker.DefaultDebounce);
        await rig.Panel.Idle.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(Codes(rig.Panel), Is.EqualTo(new[] { "e1", "i1" }),
            "a fresh result does not silently switch a hidden tier back on");
    }

    // ── AC13: the Info tier, end to end ─────────────────────────────────────────────────────────────────────

    [Test]
    public async Task TheInfoTierCountsFiltersAndSortsExactlyLikeTheOtherTwo()
    {
        using ProblemsRig rig = await MixedPanel();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.Infos.Count, Is.EqualTo(1), "it counts");
            Assert.That(Codes(rig.Panel).Last(), Is.EqualTo("i1"),
                "and it sorts LAST under the default — the advisory tier is below Warning, not above it");
        });

        rig.Panel.Infos.IsShown = false;
        Assert.That(Codes(rig.Panel), Does.Not.Contain("i1"), "it filters");

        rig.Panel.Infos.IsShown = true;
        Assert.That(Codes(rig.Panel), Does.Contain("i1"));
    }

    [Test]
    public async Task AnInfoRowRendersItsDanishLabelAndItsOwnIcon()
    {
        using ProblemsRig rig = await MixedPanel();
        ProblemRowViewModel info = rig.Panel.Rows.Single(r => r.Code == "i1");

        Assert.Multiple(() =>
        {
            Assert.That(info.TierLabel, Is.EqualTo("Information"));
            Assert.That(info.TierIcon, Is.EqualTo("/Assets/severity-info.svg"));
            Assert.That(info.Message, Is.EqualTo("En oplysning."), "verbatim, like every other tier");
        });
    }

    // ── The realized chrome ─────────────────────────────────────────────────────────────────────────────────

    [AvaloniaTest]
    public async Task TheFourTogglesAreAddressableCheckedByDefaultAndNamedInDanish()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ToggleButton[] toggles =
        [
            .. window.GetLogicalDescendants().OfType<ToggleButton>()
                .Where(t => AutomationProperties.GetAutomationId(t)?.StartsWith("problems.filter.", StringComparison.Ordinal) == true)
                .OrderBy(t => AutomationProperties.GetAutomationId(t), StringComparer.Ordinal),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(toggles.Select(AutomationProperties.GetAutomationId),
                Is.EqualTo(new[]
                {
                    "problems.filter.error", "problems.filter.fatal", "problems.filter.info",
                    "problems.filter.warning",
                }));
            Assert.That(toggles.Select(t => t.IsChecked), Has.All.EqualTo(true), "all on by default");
            Assert.That(toggles.Select(AutomationProperties.GetName),
                Is.EquivalentTo(new[] { "Fatal fejl", "Fejl", "Advarsel", "Information" }),
                "named by their Danish tier, which is also what the icon column says");
        });

        window.Close();
    }

    [AvaloniaTest]
    public async Task UncheckingAToggleReachesTheViewModel()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ToggleButton warnings = window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(t => AutomationProperties.GetAutomationId(t) == "problems.filter.warning");

        warnings.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.That(vm.Problems.Warnings.IsShown, Is.False, "the binding is two-way — the chrome drives the filter");

        window.Close();
    }
}
