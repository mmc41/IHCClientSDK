using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
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
/// The three severity filter toggles, and the Info tier they are the main way to observe.
///
/// <para><b>A count is not a row count.</b> The toggles carry LIVE counts of what the project contains, so
/// hiding a tier must not change the number beside its own button — otherwise switching a tier off makes its
/// findings appear to have been fixed, which is the one thing a filter must never imply. Counts come from the
/// bound result; the list comes from the bound result minus the hidden tiers.</para>
///
/// <para><b>Info ships EMPTY, so it is exercised synthetically.</b> No production rule emits
/// <see cref="ValidationSeverity.Info"/> today — the tier exists with its severity, icon, toggle and plumbing,
/// and a later pass populates it. That makes findings constructed here the only way to prove the tier renders,
/// counts, filters and sorts like the other two, which is exactly why these tests build their own.</para>
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

    // ── Defaults ────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task EveryTierIsShownByDefaultBecauseAHiddenFindingIsAFindingNobodyActsOn()
    {
        using ProblemsRig rig = await MixedPanel();

        Assert.Multiple(() =>
        {
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
            Assert.That(info.SeverityLabel, Is.EqualTo("Information"));
            Assert.That(info.SeverityIcon, Is.EqualTo("/Assets/severity-info.svg"));
            Assert.That(info.Message, Is.EqualTo("En oplysning."), "verbatim, like every other tier");
        });
    }

    // ── The realized chrome ─────────────────────────────────────────────────────────────────────────────────

    [AvaloniaTest]
    public async Task TheThreeTogglesAreAddressableCheckedByDefaultAndNamedInDanish()
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
                Is.EqualTo(new[] { "problems.filter.error", "problems.filter.info", "problems.filter.warning" }));
            Assert.That(toggles.Select(t => t.IsChecked), Has.All.EqualTo(true), "all on by default");
            Assert.That(toggles.Select(AutomationProperties.GetName),
                Is.EquivalentTo(new[] { "Fejl", "Advarsel", "Information" }),
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
