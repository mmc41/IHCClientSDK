using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Ihc.Tests.Shared;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The About window's repository link, as an instrumented operation.
///
/// <para><b>The defect this fixture is written against is a span that says nothing.</b> The launcher answers
/// <c>false</c> when the OS opened no handler — a machine with no browser association, the case the whole
/// boolean return exists to report — and that arm used to log and leave the span <c>Unset</c>. A support query
/// counting failed launches would have found none, because the outcome most worth counting was recorded as
/// indistinguishable from success.</para>
///
/// <para><b>And the floor is now the shared one.</b> The handler had a try/catch written out inside it, which
/// was a second copy of <c>HandlerGuard</c> — and one that had already drifted: it returned nothing for a caller
/// to react to and left no durable row.</para>
/// </summary>
public class AboutWindowFloorTests : AvaloniaTestBase
{
    private static Button RepoLink(AboutWindow about) =>
        about.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "RepoLink");

    private static async Task<(FakeDialogService Dialogs, Activity? Span)> ClickAsync(bool launcherSucceeds)
    {
        FakeDialogService dialogs = new() { OpenExternalUrlSucceeds = launcherSucceeds };
        using TelemetryCapture capture = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "AboutWindow.");
        using TraceProbe probe = TraceProbe.Start();

        AboutWindow about = new(dialogs);
        CurrentTestWindow = about;
        about.Show();
        Dispatcher.UIThread.RunJobs();

        RepoLink(about).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();

        about.Close();
        return (dialogs, probe.Spans(capture).LastOrDefault());
    }

    /// <summary>The gate's assertion: a <c>false</c> return records a FAILED span, not an unset one.</summary>
    [AvaloniaTest]
    public async Task AFalseReturnRecordsAFailedSpan()
    {
        var (dialogs, span) = await ClickAsync(launcherSucceeds: false);

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastOpenedUrl, Is.Not.Null, "non-vacuity: the launcher really was asked");
            Assert.That(span, Is.Not.Null, "the handler opened a span");
            Assert.That(span!.Status, Is.EqualTo(ActivityStatusCode.Error),
                "nothing opened, and the span says so — it used to stay Unset and read as success");
            Assert.That(span.GetTagItem("error.type"),
                Is.EqualTo(AboutWindow.RepoLinkNotOpenedOutcome),
                "coded rather than exception-shaped, because nothing threw");
        });
    }

    /// <summary>A launch that SUCCEEDS records no error — the outcome is a report, not a habit.</summary>
    [AvaloniaTest]
    public async Task ASuccessfulLaunchRecordsNoError()
    {
        var (dialogs, span) = await ClickAsync(launcherSucceeds: true);

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastOpenedUrl, Is.Not.Null);
            Assert.That(span!.Status, Is.EqualTo(ActivityStatusCode.Unset));
        });
    }
}
