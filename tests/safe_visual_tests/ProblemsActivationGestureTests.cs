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
/// The panel's TWO-TIER gesture. A single click only SELECTS — reading down a findings list may not drag the
/// trees along or open a window — and a double-click or Enter ACTIVATES, taking the installer to where the fix
/// is actually made.
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

        /// <summary>Every internal error an activation opened the details surface for, in order.</summary>
        public List<Ihc.Vis.Problems.InternalError> Shown { get; } = [];

        /// <summary>The panel's fault sink, so a test can put a fault on screen.</summary>
        public InternalErrorLog Sink { get; } = new();

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

            // A panel whose activation is RECORDED — same session, same validation, same planner wiring; only
            // the one delegate differs. It is then bound to the real list by retargeting that control's
            // DataContext, which is exactly what the view's handlers read, so the gestures below exercise the
            // shipped code path rather than a stand-in for it.
            rig.Recording = new ProblemsPanelViewModel(
                rig.Harness.Session, rig.Harness.Session.Validation,
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

        /// <summary>
        /// A panel showing ONE fault and NO findings, with no project ever opened — which is not an edge case
        /// but the case that matters most: a start-up fault is exactly the fault that arrives before there is a
        /// document, and it is the one the user most needs to be able to open.
        /// </summary>
        public static Rig ShowingAFaultWithNoProject()
        {
            Rig rig = new();
            rig.Sink.Append(new Ihc.Vis.Problems.InternalError(
                new Ihc.Vis.Problems.ProblemCode("app.openvisual.unexpected"),
                "Uventet fejl under 'Start'.", "boom", Ihc.Vis.Problems.InternalErrorOrigin.Host,
                "at Startup()", DateTimeOffset.UnixEpoch));
            rig.Recording = new ProblemsPanelViewModel(
                rig.Harness.Session, rig.Harness.Session.Validation,
                internalErrors: rig.Sink,
                showInternalError: error => { rig.Shown.Add(error); return Task.CompletedTask; });
            CurrentTestWindow = rig.Window;
            rig.Window.Show();
            Dispatcher.UIThread.RunJobs();
            rig.List.DataContext = rig.Recording;
            Dispatcher.UIThread.RunJobs();
            Assert.That(rig.Harness.Session.Current, Is.Null, "precondition: no project is open");
            Assert.That(rig.Panel.Rows, Has.Count.EqualTo(1), "sanity: the fault is on screen");
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

        /// <summary>The realized container for the single fault row.</summary>
        public TableViewRow RealizedFaultRow() =>
            Window.GetVisualDescendants().OfType<TableViewRow>()
                .First(r => r.DataContext is InternalErrorRowViewModel);

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

    /// <summary>A real left click at the control's centre, through the headless input pipeline.</summary>
    private static void Click(Window window, Avalonia.Visual target, int times = 1)
    {
        Avalonia.Point centre = target.TranslatePoint(
            new Avalonia.Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        for (int i = 0; i < times; i++)
        {
            window.MouseDown(centre, MouseButton.Left);
            window.MouseUp(centre, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A real left double-click, which is the same pipeline twice over.</summary>
    private static void DoubleClick(Window window, Avalonia.Visual target) => Click(window, target, times: 2);

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
    public async Task ASingleClickOnlySelects()
    {
        using Rig rig = await Rig.ShowingFindingsAsync();
        TableViewRow container = rig.RealizedRow(r => r.NavigationKind is not NavigationKind.None);
        object? treeBefore = rig.Shell.SelectedInstallationNode;

        Click(rig.Window, container);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SelectedRow, Is.SameAs(container.DataContext),
                "the click DID reach the row — the gesture is not ignored, it is only not a journey");
            Assert.That(rig.Activated, Is.Empty,
                "and nothing was activated — the route is the second tier of the gesture, not the first");
            Assert.That(rig.Shell.SelectedInstallationNode, Is.SameAs(treeBefore),
                "so no tree moved under the reader either");
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
        ProblemRowViewModel nowhere = rig.Panel.Rows.OfType<ProblemRowViewModel>()
            .First(r => r.NavigationKind is NavigationKind.None);

        await rig.Panel.ActivateRowAsync(nowhere);

        Assert.That(rig.Activated.Single(),
            Is.EqualTo(new NavigationPlan(null, null, NavigationKind.None)),
            "the route is stated as empty rather than silently skipped, so the executor can say so");
    }

    /// <summary>
    /// The gate's assertion: BOTH gestures open the details surface for an internal row, and they open it for
    /// the same fault. Parity is a requirement for this row kind exactly as it is for a finding row — a keyboard
    /// user must reach the detail by the same route a mouse user does.
    /// </summary>
    [AvaloniaTest]
    public void EnterAndDoubleClickBothOpenTheInternalErrorDialog()
    {
        using Rig rig = Rig.ShowingAFaultWithNoProject();
        TableViewRow container = rig.RealizedFaultRow();
        rig.Panel.SelectedRow = (ProblemsPanelRowViewModel)container.DataContext!;
        Dispatcher.UIThread.RunJobs();

        rig.FocusSelectedRow();
        rig.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        DoubleClick(rig.Window, container);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Shown, Has.Count.EqualTo(2), "both gestures opened it");
            Assert.That(rig.Shown[0], Is.EqualTo(rig.Shown[1]), "and on the same fault");
            Assert.That(rig.Shown[0].Code.Value, Is.EqualTo("app.openvisual.unexpected"));
            Assert.That(rig.Activated, Is.Empty,
                "and NOT as a navigation: a fault has no element, so the planner is never asked");
        });
    }

    /// <summary>
    /// A single click still only SELECTS. The two-tier gesture is not weakened by the new row kind: reading down
    /// the list must not throw a modal window up under the reader.
    /// </summary>
    [AvaloniaTest]
    public void ASingleClickOnAFaultRowOnlySelects()
    {
        using Rig rig = Rig.ShowingAFaultWithNoProject();
        TableViewRow container = rig.RealizedFaultRow();

        Click(rig.Window, container);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Panel.SelectedRow, Is.SameAs(container.DataContext), "it selected");
            Assert.That(rig.Shown, Is.Empty, "and opened nothing");
        });
    }
}
