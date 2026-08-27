using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
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
/// How the four states reach the screen, and how staleness is shown without becoming a flicker.
///
/// <para><b>The threshold is the design.</b> An edit→validate cycle usually completes in well under a second, so
/// marking staleness the instant it begins would blink an indicator on every keystroke — noise that trains a user
/// to ignore it. The dim engages only once staleness has PERSISTED past the threshold, which means the common
/// case shows nothing at all and the indicator only ever appears when there is really something to wait for.</para>
///
/// <para><b>Dimmed is not disabled.</b> The rows stay clickable while the panel is stale: they are the previous
/// result, they are still the best information available, and blanking or freezing them would take away the
/// navigation a user is most likely reaching for at exactly that moment.</para>
/// </summary>
public class ProblemsStatePresentationTests
{
    private static ValidationFinding Finding(string code, string message) =>
        new(new Problem(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty),
            ValidationSeverity.Warning, ValidationCategory.Documentation,
            new FindingLocation("Stue", null, null), EquatableArray<FindingLocation>.Empty);

    /// <summary>
    /// A panel whose validation run can be HELD OPEN. That matters for every staleness assertion here: the
    /// threshold (1 s) is longer than the debounce (300 ms), so advancing the clock far enough to engage the
    /// indicator also starts a run — and a run that completes clears the very state under test. Holding the run
    /// makes "stale for longer than the threshold" a fact rather than a race the test usually wins.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        private readonly System.Threading.ManualResetEventSlim _hold = new(true);
        private readonly ProblemsRig _inner;

        public Rig() => _inner = new ProblemsRig(_ =>
        {
            _hold.Wait(TimeSpan.FromSeconds(10));
            return System.Collections.Immutable.ImmutableArray.Create(
                Finding("doc-name-empty", "Navnet mangler."));
        });

        public ShellHarness Harness => _inner.Harness;

        public FakeTimeProvider Clock => _inner.Clock;

        public ProblemsPanelViewModel Panel => _inner.Panel;

        /// <summary>Blocks the next run inside the validate delegate until <see cref="Release"/>.</summary>
        public void HoldRuns() => _hold.Reset();

        public void Release() => _hold.Set();

        public Task SettleAsync() => _inner.SettleAsync();

        public void Dispose()
        {
            Release();
            _inner.Dispose();
            _hold.Dispose();
        }
    }

    private static async Task<Rig> ValidatedShell()
    {
        Rig rig = new();
        await rig.Harness.Session.NewAsync();
        await rig.SettleAsync();
        return rig;
    }

    private static Control ById(Window window, string id) =>
        window.GetLogicalDescendants().OfType<Control>()
            .Single(c => AutomationProperties.GetAutomationId(c) == id);

    // ── The named constants ─────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void TheThreeStalenessConstantsAreNamedSoTheyCanBeTunedInOnePlace()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProblemsPanelViewModel.StaleIndicatorDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(ProblemsPanelViewModel.StaleOpacity, Is.EqualTo(0.5).Within(0.001));
            Assert.That(ProblemsPanelViewModel.StaleFadeDuration, Is.EqualTo(TimeSpan.FromMilliseconds(150)));
        });
    }

    [Test]
    public void TheTwoStateTextsAreExactlyTheSpecStringsIncludingTheEllipsis()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProblemsPanelViewModel.ValidatingText, Is.EqualTo("Validerer projektet…"),
                "one ellipsis character, not three periods");
            Assert.That(ProblemsPanelViewModel.CleanText, Is.EqualTo("Ingen problemer fundet"));
        });
    }

    // ── Opacity ─────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AnUpToDatePanelIsAtFullContrast()
    {
        using Rig rig = await ValidatedShell();

        Assert.That(rig.Panel.RowsOpacity, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public async Task ASubSecondEditValidateCycleNeverDims()
    {
        using Rig rig = await ValidatedShell();

        rig.HoldRuns();
        await rig.Harness.Session.ApplyAsync(new Ihc.Vis.Session.AddLocality("Ny stue"));
        rig.Clock.Advance(ProblemsPanelViewModel.StaleIndicatorDelay - TimeSpan.FromMilliseconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.State, Is.EqualTo(ProblemsState.Stale), "it IS stale");
            Assert.That(rig.Panel.IsStaleIndicatorEngaged, Is.False, "but it does not say so yet");
            Assert.That(rig.Panel.RowsOpacity, Is.EqualTo(1.0).Within(0.001), "and nothing dims");
        });
    }

    [Test]
    public async Task PastTheThresholdTheRowAreaDimsToTheNamedOpacity()
    {
        using Rig rig = await ValidatedShell();

        rig.HoldRuns();
        await rig.Harness.Session.ApplyAsync(new Ihc.Vis.Session.AddLocality("Ny stue"));
        rig.Clock.Advance(ProblemsPanelViewModel.StaleIndicatorDelay);

        Assert.That(rig.Panel.RowsOpacity, Is.EqualTo(ProblemsPanelViewModel.StaleOpacity).Within(0.001));

        rig.Release();
        await rig.SettleAsync();
        Assert.That(rig.Panel.RowsOpacity, Is.EqualTo(1.0).Within(0.001), "and it comes back when the result binds");
    }

    // ── The realized panel ──────────────────────────────────────────────────────────────────────────────────

    [AvaloniaTest]
    public async Task TheStateTextIsShownOverTheListAndCarriesTheDanishSentence()
    {
        FakeTimeProvider clock = new();
        using ShellHarness harness = ShellHarness.Create(clock);
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        TextBlock state = (TextBlock)ById(window, "ProblemsStateText");

        Assert.Multiple(() =>
        {
            Assert.That(state.IsVisible, Is.True, "nothing is bound yet, so the panel says what it is doing");
            Assert.That(state.Text, Is.EqualTo(ProblemsPanelViewModel.ValidatingText));
            Assert.That(state.Text, Is.Not.EqualTo(ProblemsPanelViewModel.CleanText),
                "an unvalidated project must never read as problem-free");
        });

        window.Close();
    }

    [AvaloniaTest]
    public async Task TheHeaderSpinnerAppearsOnlyWhileTheIndicatorIsEngaged()
    {
        FakeTimeProvider clock = new();
        using ShellHarness harness = ShellHarness.Create(clock);
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        clock.Advance(ValidationWorker.DefaultDebounce);
        await vm.Problems.Idle.WaitAsync(TimeSpan.FromSeconds(10));
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control spinner = ById(window, "ProblemsSpinner");
        Assert.That(spinner.IsVisible, Is.False, "up to date — nothing is spinning");

        // The flag is set directly rather than by advancing past the threshold, and the reason is that the
        // threshold (1 s) is longer than the debounce (300 ms): any advance that engages the indicator also
        // starts a run, and a completed run clears the flag again. WHEN the flag turns on is pinned by the
        // view-model tests above, on a held run; what this test owns is that the header FOLLOWS it.
        vm.Problems.IsStaleIndicatorEngaged = true;
        Dispatcher.UIThread.RunJobs();

        Assert.That(spinner.IsVisible, Is.True, "engaged — the header says a run is outstanding");

        window.Close();
    }

    /// <summary>
    /// The fade is a TRANSITION on the row area, not an instant jump. Asserting the transition exists (and at the
    /// named duration) is what ties the markup to the constant; asserting an interpolated value mid-flight would
    /// be timing-dependent and would tell us less.
    /// </summary>
    [AvaloniaTest]
    public async Task TheDimIsAnimatedRatherThanInstantInBothDirections()
    {
        using ShellHarness harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Control rows = ById(window, "ProblemsRowArea");
        DoubleTransition? fade = rows.Transitions?
            .OfType<DoubleTransition>()
            .FirstOrDefault(t => t.Property == Visual.OpacityProperty);

        Assert.Multiple(() =>
        {
            Assert.That(fade, Is.Not.Null, "a transition, so the opacity change is a fade rather than a jump");
            Assert.That(fade!.Duration, Is.EqualTo(ProblemsPanelViewModel.StaleFadeDuration),
                "and at the named duration — a transition is symmetric, so this is the fade in BOTH directions");
        });

        window.Close();
    }

    [AvaloniaTest]
    public async Task RowsStayClickableWhileTheListIsDimmed()
    {
        FakeTimeProvider clock = new();
        using ShellHarness harness = ShellHarness.Create(clock);
        using MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        // A fixture rather than the starter project: this test is about the ROWS surviving the dim, and a
        // healthy new project carries no findings to survive it.
        await harness.Session.OpenAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));
        clock.Advance(ValidationWorker.DefaultDebounce);
        await vm.Problems.Idle.WaitAsync(TimeSpan.FromSeconds(10));
        Window window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Same reason as the spinner test: the engaged state is set directly, because reaching it through the
        // clock would also complete the run that clears it.
        vm.Problems.IsStaleIndicatorEngaged = true;
        Dispatcher.UIThread.RunJobs();

        Control rows = ById(window, "ProblemsRowArea");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Problems.IsStaleIndicatorEngaged, Is.True, "precondition: dimmed");
            Assert.That(rows.IsHitTestVisible, Is.True,
                "a stale list is the best information there is; taking the clicks away would remove the "
                + "navigation a user is most likely reaching for right then");
            Assert.That(rows.IsEnabled, Is.True);
            Assert.That(vm.Problems.Rows, Is.Not.Empty, "and the rows are still there — the list is never blanked");
        });

        window.Close();
    }
}
