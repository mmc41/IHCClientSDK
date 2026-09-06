using System;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Tests;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The Problemer panel's export is the ONE gesture that reaches a workflow without passing the shell's command
/// boundary: it is an <c>AsyncRelayCommand</c> the panel constructs itself, deliberately, because the toolkit's
/// CanExecute predicate would compete with the command registry's Gate.
///
/// <para>That leaves a question a headless view-model test cannot answer, because the answer belongs to the
/// dispatcher: a fault raised BEFORE the workflow's own <c>try</c> — its argument guard, its scope — has no
/// catch of its own anywhere on the way out. Measured here rather than reasoned from the toolkit's
/// documentation, and the measurement says it is contained: <c>ICommand.Execute</c> lets such a fault out
/// SYNCHRONOUSLY, so it lands on the dispatcher that raised the click and the application's own net records it.
/// The record is a generic fault rather than <i>Eksport mislykkedes</i>, which is the honest description of
/// what happened — nothing got as far as the operation that would have worded it.</para>
/// <para>Pinned because the containment is incidental rather than designed: it rests on
/// <c>AsyncRelayCommand</c>'s default option, and the day this gesture is posted, awaited or given
/// <c>FlowExceptionsToTaskScheduler</c>, the fault has nowhere left to go.</para>
/// </summary>
[TestFixture]
public class ExportCommandContainmentTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task AFaultInTheExportGesture_ReachesTheApplicationsFaultRecord()
    {
        using CapturedFaults faults = new();
        // The application's OWN dispatcher net, installed the way Program.Main installs it — otherwise this
        // would measure the absence of a handler rather than the presence of a gap.
        CapturingLogger logger = new();
        DispatcherUnhandledExceptionEventHandler net = Ihc.Bootstrap.AppTelemetryBootstrap.DispatcherExceptionHandler(
            logger, fault => TaskSupervisor.Report(fault, "Dispatcher.UnhandledException"));
        Dispatcher.UIThread.UnhandledException += net;
        // Removed in a FINALLY, because the dispatcher outlives this test: the handler is installed on the one
        // UI thread the whole suite shares, so a failure anywhere below would leave every later test running
        // under this test's net — reporting its faults here, and to a sink this method has already disposed.
        try
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();

            using ValidationMonitor monitor = new(harness.Session, p => harness.Session.ValidateStructured(p));
            using ProblemsPanelViewModel panel = new(
                harness.Session, monitor,
                export: _ => throw new TimeoutException("the export gesture faulted before its own guard"));

            // The BUTTON's path, whole: ICommand.Execute (not ExecuteAsync, which hands the fault to an awaiter
            // no button has) raised from the dispatcher, which is where a click arrives.
            Dispatcher.UIThread.Post(() => panel.ExportCommand.Execute(null));
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= net;
        }

        Assert.That(faults.Rows, Is.Not.Empty,
            "a fault in the panel's own command has to leave a record; nothing above it catches one");
    }
}
