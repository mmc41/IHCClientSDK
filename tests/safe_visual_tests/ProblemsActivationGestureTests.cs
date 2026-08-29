using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;

namespace safe_visual_tests;

/// <summary>
/// The panel's TWO-TIER gesture. A single click reveals — that is what a list selection already does — and a
/// double-click or Enter ACTIVATES, taking the installer to where the fix is actually made.
///
/// <para>Enter parity is a requirement rather than a nicety: a keyboard user must reach the fix context by the
/// same route a mouse user does. Both gestures therefore go through one entry point, and these tests assert on
/// what that produced rather than on which handler ran.</para>
/// </summary>
public class ProblemsActivationGestureTests : AvaloniaTestBase
{
    private sealed class Rig : IDisposable
    {
        public FakeTimeProvider Clock { get; } = new();
        public ShellHarness Harness { get; }
        public MainWindowViewModel Shell { get; }
        public MainWindow Window { get; }

        /// <summary>Every plan an activation carried out, in order.</summary>
        public List<NavigationPlan> Activated { get; } = [];

        /// <summary>Every id a reveal was asked for — the FIRST tier, which activation must not disturb.</summary>
        public List<Ihc.Vis.Model.ElementId> Revealed { get; } = [];

        private Rig()
        {
            Harness = ShellHarness.Create(Clock);
            Shell = Harness.CreateViewModel();
            Window = new MainWindow { DataContext = Shell };
        }

        public static async Task<Rig> ShowingFindingsAsync()
        {
            Rig rig = new();
            await rig.Shell.InitializeAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));

            // A panel whose activation and reveal are RECORDED — same session, same validation, same planner
            // wiring; only the two delegates differ. It is then bound to the real list by retargeting that
            // control's DataContext, which is exactly what the view's handlers read, so the gestures below
            // exercise the shipped code path rather than a stand-in for it.
            rig.Recording = new ProblemsPanelViewModel(
                rig.Harness.Session, rig.Harness.Session.Validation,
                reveal: id => { rig.Revealed.Add(id); return true; },
                activate: plan => { rig.Activated.Add(plan); return Task.CompletedTask; });

            rig.Clock.Advance(ValidationWorker.DefaultDebounce);
            await rig.Recording.Idle.WaitAsync(TimeSpan.FromSeconds(30));
            CurrentTestWindow = rig.Window;
            rig.Window.Show();
            Dispatcher.UIThread.RunJobs();
            rig.List.DataContext = rig.Recording;
            Dispatcher.UIThread.RunJobs();
            Assert.That(rig.Panel.Rows, Is.Not.Empty, "sanity: the fixture produced findings to activate");
            return rig;
        }

        public ProblemsPanelViewModel Recording { get; private set; } = null!;

        public ProblemsPanelViewModel Panel => Recording;

        public Control List => Window.GetVisualDescendants().OfType<Control>()
            .First(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "ProblemsList");

        /// <summary>
        /// A row that is actually REALIZED, so a pointer gesture can reach it. The list virtualizes, so picking
        /// by view-model order would often name a row that has no container to click.
        /// </summary>
        public TableViewRow RealizedRow(Func<ProblemRowViewModel, bool> wanted) =>
            Window.GetVisualDescendants().OfType<TableViewRow>()
                .First(r => r.DataContext is ProblemRowViewModel row && wanted(row));

        /// <summary>
        /// Puts keyboard focus on the selected ROW, not on the list. A key event is routed from whatever holds
        /// focus and bubbles upward, so focusing the list itself — which is not focusable — leaves the keystroke
        /// travelling to the window and never through the handler under test.
        /// </summary>
        public void FocusSelectedRow()
        {
            TableViewRow container = Window.GetVisualDescendants().OfType<TableViewRow>()
                .First(r => ReferenceEquals(r.DataContext, Panel.SelectedRow));
            container.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.That(List.IsKeyboardFocusWithin, Is.True,
                "precondition: the keystroke must originate inside the list, or it never reaches the panel");
        }

        public void Dispose()
        {
            Window.Close();
            Shell.Dispose();
            Harness.Dispose();
        }
    }

    /// <summary>A real left double-click at the control's centre, through the headless input pipeline.</summary>
    private static void DoubleClick(Window window, Avalonia.Visual target)
    {
        Avalonia.Point centre = target.TranslatePoint(
            new Avalonia.Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        for (int i = 0; i < 2; i++)
        {
            window.MouseDown(centre, MouseButton.Left);
            window.MouseUp(centre, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaTest]
    public async Task EnterAndDoubleClickProduceTheIdenticalActivation()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();
        // ONE realized row, driven by both gestures. Clicking the list's centre would land on whichever row sits
        // there and compare two activations of different findings.
        TableViewRow container = rig.RealizedRow(r => r.NavigationKind is not NavigationKind.None);
        ProblemRowViewModel row = (ProblemRowViewModel)container.DataContext!;
        rig.Panel.SelectedRow = row;
        Dispatcher.UIThread.RunJobs();

        rig.FocusSelectedRow();
        rig.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        DoubleClick(rig.Window, container);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Activated, Has.Count.EqualTo(2), "both gestures activated");
            Assert.That(rig.Activated[0], Is.EqualTo(rig.Activated[1]),
                "the SAME plan — a keyboard user reaches the same place a mouse user does");
            Assert.That(rig.Activated[0].Kind, Is.EqualTo(row.NavigationKind),
                "and it is the route the row promised before the click");
        });
    }

    [AvaloniaTest]
    public async Task ASingleClickStillOnlyReveals()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();

        rig.Panel.SelectedRow = rig.Panel.Rows.First(r => r.Element is not null);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Revealed, Is.Not.Empty, "selecting a row reveals its element, as it always did");
            Assert.That(rig.Activated, Is.Empty,
                "and it does NOT activate — the deep route is the second tier of the gesture, not the first");
        });
    }

    /// <summary>
    /// Enter is the shell's default-button gesture. If the panel let it bubble, one keystroke would both activate
    /// a finding and press whatever default button the surrounding window has.
    /// </summary>
    [AvaloniaTest]
    public async Task EnterOnARowDoesNotAlsoReachADefaultButton()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();
        int pressed = 0;
        Button spy = new() { IsDefault = true };
        spy.Click += (_, _) => pressed++;
        ((Panel)rig.Window.Content!).Children.Add(spy);
        Dispatcher.UIThread.RunJobs();

        rig.Panel.SelectedRow = (ProblemRowViewModel)rig
            .RealizedRow(r => r.NavigationKind is not NavigationKind.None).DataContext!;
        Dispatcher.UIThread.RunJobs();
        rig.FocusSelectedRow();
        rig.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Activated, Has.Count.EqualTo(1), "the row was activated");
            Assert.That(pressed, Is.Zero, "and the default button was not pressed by the same keystroke");
        });
    }

    /// <summary>Activating a row that leads nowhere carries out a plan that does nothing, rather than nothing at all.</summary>
    [AvaloniaTest]
    public async Task ActivatingARowThatLeadsNowhereStillProducesItsHonestPlan()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();
        ProblemRowViewModel nowhere = rig.Panel.Rows.First(r => r.NavigationKind is NavigationKind.None);

        await rig.Panel.ActivateRowAsync(nowhere);

        Assert.That(rig.Activated.Single(),
            Is.EqualTo(new NavigationPlan(null, null, NavigationKind.None)),
            "the route is stated as empty rather than silently skipped, so the executor can say so");
    }
}
