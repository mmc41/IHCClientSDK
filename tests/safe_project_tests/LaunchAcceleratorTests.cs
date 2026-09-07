using System;
using System.IO;
using System.Threading.Tasks;
using ihc_openvisual;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The two things <c>Main</c> starts purely to make a launch faster — the multicore-JIT profile and the
/// catalog warm-up. Neither changes what the application does, which is exactly why nothing else would notice
/// if one of them quietly stopped being correct.
/// </summary>
[TestFixture]
public class LaunchAcceleratorTests
{
    /// <summary>
    /// The JIT profile is keyed to this machine's hardware and to these exact binaries, so it belongs under
    /// LOCAL application data. Put in the roaming folder it would be synchronised at every logon and discarded
    /// on arrival — a failure mode nothing surfaces, because a stale or foreign profile simply stops matching
    /// and the application still runs, merely slower.
    /// </summary>
    /// <remarks>
    /// Asserted against the two folders as the framework resolves them rather than against a spelling: the
    /// whole reason <c>Constants.LocalAppDataPath</c> exists beside <c>AppDataPath</c> is that the two are
    /// different folders, and until this ran nothing said which door each caller had taken.
    /// </remarks>
    [Test]
    public void TheJitProfileIsLocal_AndTheStartupBreadcrumbRoams()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // macOS resolves BOTH special folders to ~/Library/Application Support, so there is no roaming folder to
        // stay out of and the door a caller took leaves no trace: the assertions below would hold whichever one
        // it was. Nothing here is platform-specific — both paths come from the same source on every platform —
        // so the Windows and Linux runs, where the folders differ, pin the distinction for all three.
        if (local == roaming)
        {
            Assert.Ignore("this platform has one application-data folder, so local and roaming cannot be told apart");
        }

        Assert.Multiple(() =>
        {
            Assert.That(Program.JitProfileDirectory(), Does.StartWith(local + Path.DirectorySeparatorChar),
                "a profile keyed to this machine's binaries must not follow the user to another machine");
            Assert.That(Program.DefaultLastResortPath(), Does.StartWith(roaming + Path.DirectorySeparatorChar),
                "the breadcrumb from a failed launch is the user's, and roams with them");
        });
    }

    /// <summary>
    /// A started warm-up is KEPT and waited for, rather than discarded. It runs on a pool thread and reports
    /// through the telemetry pipeline and logger factory that <c>Main</c>'s <c>finally</c> tears down, so on a
    /// launch that fails early the two are genuinely concurrent: without a handle there is no way to order
    /// them, and shutting the pipeline down under a running warm-up drops the materialization span and points
    /// that thread's own fault handler at a disposed logger factory.
    /// </summary>
    /// <remarks>
    /// The handle is asserted separately from the wait because the wait alone cannot fail: a warm-up nobody
    /// kept leaves nothing outstanding, so "nothing is outstanding" would be true for the wrong reason.
    /// <para>This materializes the embedded catalog if no earlier fixture already did, which is the point —
    /// the wait has to be able to cover real work rather than an already-finished task.</para>
    /// </remarks>
    [Test]
    public void AStartedWarmupIsKept_AndWaitedForBeforeThePipelineItReportsThroughGoesDown()
    {
        Program.StartCatalogWarmup(NullLogger.Instance);
        Task? outstanding = Program.OutstandingCatalogWarmup;

        bool settled = Program.AwaitCatalogWarmup(Program.CatalogWarmupShutdownLimit);

        Assert.Multiple(() =>
        {
            Assert.That(outstanding, Is.Not.Null,
                "a discarded warm-up cannot be ordered against the shutdown that disposes what it reports to");
            Assert.That(settled, Is.True, "the wait ran out with the warm-up still going");
            Assert.That(outstanding?.IsCompleted, Is.True,
                "the wait returned while the pool thread was still inside the materialization");
        });
    }
}
