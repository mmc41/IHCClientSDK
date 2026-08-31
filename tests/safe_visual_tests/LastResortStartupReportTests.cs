using System;
using System.IO;
using System.Linq;
using ihc_openvisual;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// What a fatal start-up leaves behind when the logging pipeline never came up.
///
/// <para>The application is a <c>WinExe</c>: it has no console, so <c>Trace</c> output reaches nobody unless a
/// debugger happens to be attached. <c>Main</c> returns <c>void</c>, so after catching a fatal start-up error the
/// process exited <b>0</b> — a launcher, a script or a support engineer reading the exit code was told the run
/// succeeded. Between them, an installer double-clicked the application, no window appeared, and nothing anywhere
/// recorded why.</para>
///
/// <para>The report is EXTRACTED from <c>Main</c> deliberately. <c>Main</c> is <c>void</c> in a windowed app, so
/// no in-process test can observe a real process exit; a gate phrased against one would be wishful. What can be
/// tested is the decision — what gets written, where, and which exit code is chosen — so that is what is a
/// member.</para>
/// </summary>
[TestFixture]
public class LastResortStartupReportTests
{
    private const string ScratchPrefix = "ihc_ov_lastresort_";

    /// <summary>Reproduce-first: with no logging pipeline there was no record and no non-zero exit code.</summary>
    [Test]
    public void WithNoLoggerFactory_TheFaultIsWrittenToTheLastResortPath_AndTheExitCodeIsNonZero()
    {
        using ScratchDir dir = new(ScratchPrefix);
        string path = dir.File("startup-error.log");

        int exitCode = Program.ReportFatalStartup(
            new InvalidOperationException("the telemetry pipeline could not be built"), loggerFactory: null, path);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Not.Zero, "a fatal start-up must not report success to whatever launched it");
            Assert.That(File.Exists(path), Is.True, "the one record this run will ever have must actually be written");
            Assert.That(File.ReadAllText(path), Does.Contain("the telemetry pipeline could not be built"),
                "and it must say what happened, not merely that something did");
        });
    }

    /// <summary>With the pipeline up, the fault goes through it — the last-resort file is for the case where
    /// there is no pipeline, and writing both would put the same fault in two places with different lifetimes.</summary>
    [Test]
    public void WithALoggerFactory_TheFaultIsLogged_AndNoLastResortFileIsWritten()
    {
        using ScratchDir dir = new(ScratchPrefix);
        string path = dir.File("startup-error.log");
        var logs = new CapturingLoggerFactory();

        int exitCode = Program.ReportFatalStartup(new InvalidOperationException("boom"), logs, path);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Not.Zero, "the exit code does not depend on whether a logger was up");
            Assert.That(logs.Messages.Any(m => m.StartsWith("Critical:")), Is.True,
                "a fatal start-up is Critical, not Error");
            Assert.That(File.Exists(path), Is.False, "the last-resort write is for the case with no pipeline");
        });
    }

    /// <summary>The last-resort write is itself best-effort. It runs on a path nobody has validated, in a process
    /// that is already failing, so a write that cannot land must not replace the original fault with its own.</summary>
    [Test]
    public void AnUnwritableLastResortPath_DoesNotThrow()
    {
        using ScratchDir dir = new(ScratchPrefix);
        string path = dir.File("startup-error.log");
        Directory.CreateDirectory(path);   // a directory where the file belongs

        Assert.DoesNotThrow(
            () => Program.ReportFatalStartup(new InvalidOperationException("boom"), loggerFactory: null, path),
            "a failing breadcrumb must not become the failure that is reported");
    }

    /// <summary>The default path is under the user's own application data, beside the preference files, rather
    /// than beside the executable — which on a real installation is frequently not writable.</summary>
    [Test]
    public void TheDefaultLastResortPath_IsUnderTheUsersApplicationData()
    {
        string path = Program.DefaultLastResortPath();

        Assert.That(path, Does.StartWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            "an installation directory is often read-only; the per-user data folder is where this app already writes");
    }
}
