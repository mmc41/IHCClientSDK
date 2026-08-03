using System;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The exception route no global handler covers (Avalonia logging review AP-06/WS-11): window-lifecycle handlers
/// are invoked directly from the window message loop, so a throw inside <c>Closing</c> reaches neither
/// <c>Dispatcher.UnhandledException</c> nor <c>AppDomain.UnhandledException</c> — it just kills the app with no
/// record. The same applies to any <c>async void</c> handler: its fault is raised on the synchronization context
/// with nothing awaiting it.
/// <para>These pin the containment: the failure is caught where it happens, recorded through ILogger (so it is
/// still OTLP-exported), and — for the quit path specifically — the quit is CANCELLED, because a save prompt that
/// failed cannot be read as "the installer chose to discard".</para>
/// </summary>
public class WindowLifecycleFaultTests : AvaloniaTestBase
{
    /// <summary>The quit decision itself, with no window in play: a failed save prompt logs and answers "cannot
    /// close". Answering true would throw away exactly the unsaved work the prompt existed to protect.</summary>
    [Test]
    public async Task CanCloseAsync_WhenTheSavePromptThrows_CancelsTheQuitAndLogs()
    {
        using var harness = ShellHarness.Create();
        var logs = new CapturingLoggerFactory();
        MainWindowViewModel vm = harness.CreateViewModel(logs);
        await vm.InitializeAsync();
        await harness.Session.AddLocalityAsync();
        Assert.That(harness.Session.IsDirty, Is.True, "precondition: the close path must reach the save prompt");
        harness.Dialogs.ConfirmSaveChangesThrows = new InvalidOperationException("closing-boom-42");

        bool canClose = await vm.CanCloseAsync();

        Assert.Multiple(() =>
        {
            Assert.That(canClose, Is.False, "a failed save prompt cancels the quit rather than discarding the work");
            Assert.That(logs.Messages, Has.Some.Contains("closing-boom-42"),
                "and the failure is recorded through ILogger rather than vanishing with the process");
        });
    }

    /// <summary>The same fault driven through the real <c>Closing</c> handler: it must not escape into the message
    /// loop, and the window stays open.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Closing_WhenTheSavePromptThrows_IsContainedAndKeepsTheWindowOpen()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel(new CapturingLoggerFactory());
        await vm.InitializeAsync();
        await harness.Session.AddLocalityAsync();
        harness.Dialogs.ConfirmSaveChangesThrows = new InvalidOperationException("closing-boom-42");

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(() => window.Close(), Throws.Nothing, "the fault does not escape the Closing handler");
        Dispatcher.UIThread.RunJobs();

        Assert.That(window.IsVisible, Is.True,
            "and the window stays open — a failed save prompt must not discard unsaved work");
    }

    /// <summary>The containment primitive the remaining <c>async void</c> handlers (tree drag-source, tree drop,
    /// the pin dialog's Apply) share. It never rethrows, records what it caught, and reports the fault back so a
    /// caller can react — proved in both directions so it cannot pass by simply swallowing everything.</summary>
    [Test]
    public async Task HandlerGuard_ContainsAndLogsAFault_AndStaysSilentOnSuccess()
    {
        var logs = new CapturingLoggerFactory();

        Exception? clean = await HandlerGuard.RunAsync(() => Task.CompletedTask, logs.Logger, "CleanHandler");
        Exception? faulted = await HandlerGuard.RunAsync(
            () => throw new InvalidOperationException("handler-boom-42"), logs.Logger, "FaultyHandler");

        Assert.Multiple(() =>
        {
            Assert.That(clean, Is.Null, "a handler that completes reports no fault");
            Assert.That(logs.Messages, Has.None.Contains("CleanHandler"), "and logs nothing");
            Assert.That(faulted, Is.Not.Null.And.Message.Contains("handler-boom-42"),
                "a faulted handler's exception is returned, not rethrown into the message loop");
            Assert.That(logs.Messages, Has.Some.Contains("handler-boom-42").And.Some.Contains("FaultyHandler"),
                "and is recorded with the handler that raised it");
        });
    }
}
