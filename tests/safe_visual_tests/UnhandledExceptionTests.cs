using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Configuration;
using Ihc;
using Ihc.Bootstrap;
using Ihc.Vis;
using Ihc.Vis.Projects;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>A-25/US-063: the least-recoverable failure (an unhandled exception) is recorded through <see cref="ILogger"/>
/// — the same pipeline as command-scoped errors, not a bare <c>Trace</c> — and the atomic project save never leaves a
/// partially written <c>.vis</c>.</summary>
public class UnhandledExceptionTests
{
    // CapturingLogger (a real ILogger recording its output — never a mock) now lives in TestSupport.cs, shared with
    // GlobalExceptionHandlerTests.

    [Test]
    public async Task UnhandledException_LoggedAndNoPartialVis()
    {
        // (a) The unhandled-exception handler records the failure through ILogger.
        var logger = new CapturingLogger();
        AppTelemetryBootstrap.LogUnhandledException(logger, new InvalidOperationException("boom-42"));
        Assert.That(logger.Messages, Has.Some.Contains("boom-42"), "the unhandled exception reaches ILogger");
        Assert.That(logger.Messages, Has.Some.Contains("Critical"), "and at a critical level");

        // (b) A save interrupted by an error leaves no partially written .vis — the target keeps its prior content.
        using var harness = ShellHarness.Create();
        var svc = new ProjectAppService(new IhcSettings());
        Project project = svc.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
        string path = harness.TempPath("project.vis");
        await svc.SaveDocument(project, path);
        byte[] intact = File.ReadAllBytes(path);

        // Break the swap the save ends in, with the target already on disk, so the failure lands AFTER the new
        // bytes have been written — which is the only moment a half-written .vis could appear. A directory
        // occupying the .BAK name fails the swap identically on Windows and on POSIX (measured on both).
        // The previous mechanism — holding the target open with FileShare.None — is a Windows guarantee only:
        // POSIX rename(2) ignores open handles, so on Linux the second save simply SUCCEEDED and every assertion
        // below was reached with the wrong precondition.
        Directory.CreateDirectory(Path.ChangeExtension(path, ".BAK"));
        Assert.That(async () => await svc.SaveDocument(project, path), Throws.InstanceOf<IOException>(),
            "the save fails when it cannot complete its swap");

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(intact), "the .vis is untouched — never partially written");
            Assert.That(Directory.GetFiles(harness.TempDir, "*.vis"), Is.EqualTo(new[] { path }),
                "no partial .vis is left behind by the failed save");
        });
    }
}
