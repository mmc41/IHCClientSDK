using System;
using Avalonia.Threading;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The whole shell over the authored error corpus, in a SHOWN window. The shape for anything that measures what a
/// reader actually gets — realized rows, their automation identities, the panel's type and spacing — because none
/// of that exists until a window has been shown and the dispatcher has run.
/// </summary>
/// <remarks>
/// <para>Composes <see cref="ProblemsShellRig"/> and adds only the window, so the harness, the fake clock and the
/// settle protocol stay defined once.</para>
/// <para>Only ONE window is ever shown: under Avalonia headless a second window renders blank, which reads at the
/// assertion as content that failed to load rather than as a rig that showed too much.</para>
/// </remarks>
internal sealed class ProblemsWindowRig : IDisposable
{
    private readonly ProblemsShellRig _inner = new();

    public FakeTimeProvider Clock => _inner.Clock;

    public ShellHarness Harness => _inner.Harness;

    public MainWindowViewModel Shell => _inner.Shell;

    public MainWindow Window { get; }

    private ProblemsWindowRig()
    {
        Window = new MainWindow { DataContext = Shell };
    }

    /// <summary>Loads the authored error fixture, settles the validation run and shows the window.</summary>
    public static async Task<ProblemsWindowRig> ShowingFindingsAsync()
    {
        ProblemsWindowRig rig = new();
        await rig.Shell.InitializeAsync(ProblemsTestData.FixturePath("Project6-Errors.vis"));
        await rig._inner.SettleAsync();

        // The screenshot-on-failure hook: a failing test over this rig produces a PNG of THIS window.
        AvaloniaTestBase.CurrentTestWindow = rig.Window;
        rig.Window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(rig.Shell.Problems.Rows, Is.Not.Empty,
            "sanity: the fixture must produce findings, or every assertion over this rig is vacuous");
        return rig;
    }

    public void Dispose()
    {
        Window.Close();
        _inner.Dispose();
    }
}
