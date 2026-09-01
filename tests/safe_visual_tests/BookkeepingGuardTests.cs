using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ihc.Vis.Problems;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// An operation is finished when its DURABLE EFFECT has landed — the bytes on disk, the definition registered,
/// the document open. Everything after that is the application's own bookkeeping: marking clean, adopting a path,
/// adding a recent entry, repainting. A fault in the bookkeeping says nothing about whether the operation
/// happened, so it may not be reported as though the operation failed.
///
/// <para><b>The defect family these pin.</b> One <c>try</c> spanned both, so any fault after the durable effect
/// was worded as the operation's own failure. Measured on the save path before the fix: a complete 5761-byte
/// project on disk, a dialog saying it could not be saved, and an empty Problemer panel — because a coded
/// operation outcome does not reach the internal-error sink. The natural next move is to redo the work that
/// already succeeded.</para>
///
/// <para><b>Why a throwing event subscriber is the seam.</b> The last bookkeeping step of each of these
/// operations raises an event, and the subscribers are real: <c>StateChanged</c> drives the whole title/tree
/// rebuild and the validation monitor, and <c>CatalogChanged</c> re-projects the just-imported definition into
/// two bound collections. Nothing is mocked here — every one of these is a subscriber the shell itself
/// installs.</para>
/// </summary>
[TestFixture]
public class BookkeepingGuardTests
{
    private const string Boom = "a repaint handler broke";

    private static Exception Breaking() => new InvalidOperationException(Boom);

    /// <summary>A harness whose faults are collected, with a document already open.</summary>
    private static async Task<(ShellHarness Harness, List<InternalError> Faults)> StartedAsync()
    {
        List<InternalError> faults = [];
        ShellHarness harness = ShellHarness.Create(faultSink: faults.Add);
        await harness.Session.NewAsync();
        harness.Dialogs.Reset();
        faults.Clear();
        return (harness, faults);
    }

    /// <summary>A real project file, written by the app itself so it is genuinely loadable.</summary>
    private static async Task<string> AProjectFileAsync(ShellHarness harness, string name)
    {
        string path = harness.TempPath(name);
        harness.Dialogs.SavePath = path;
        await harness.Session.SaveAsAsync();
        return path;
    }

    // The shared oracle fixtures, copied next to the test assembly by tests\TestData.props.
    private static string SampleProductDef() =>
        Path.Combine(TestContext.CurrentContext.TestDirectory,
            "testdata", "products", "synthetic", "synthetic_9f01_input.def");

    // ---------- Save ----------

    /// <summary>The bytes are there, and nothing claims otherwise.</summary>
    [Test]
    public async Task ASaveWhoseBookkeepingBreaksIsNotReportedAsAFailure()
    {
        (ShellHarness harness, List<InternalError> faults) = await StartedAsync();
        using (harness)
        {
            string path = harness.TempPath("bookkeeping.vis");
            harness.Dialogs.SavePath = path;
            harness.Session.StateChanged += (_, _) => throw Breaking();

            await harness.Session.SaveAsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.True, "precondition: the write itself completed");
                Assert.That(harness.Dialogs.LastProblem?.Code.Value,
                    Is.Not.EqualTo("app.openvisual.project-save-failed"),
                    "the file was written, so nothing may claim it was not");
                Assert.That(faults, Is.Not.Empty, "and the real cause is recorded as a fault in the tool");
                Assert.That(faults[0].Detail, Does.Contain(Boom));
            });
        }
    }

    /// <summary>
    /// A broken subscriber may not stop the LATER bookkeeping steps. They are independent, and the repaint is
    /// last: under one shared guard a fault in the recent-list store skipped it, so the window kept the old title
    /// and the unsaved marker over a save that had succeeded.
    /// </summary>
    [Test]
    public async Task ASaveWhoseRecentListBreaksStillRepaints()
    {
        (ShellHarness harness, List<InternalError> faults) = await StartedAsync();
        using (harness)
        {
            harness.Dialogs.SavePath = harness.TempPath("still-repaints.vis");
            // The recent-list store announces through its own event, which is where the fault comes from —
            // BEFORE the repaint in the save's order of work.
            harness.Recent.Changed += (_, _) => throw Breaking();
            int repaints = 0;
            harness.Session.StateChanged += (_, _) => repaints++;

            bool saved = await harness.Session.SaveAsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(saved, Is.True);
                Assert.That(repaints, Is.GreaterThan(0),
                    "the repaint must still run, or the shell shows the pre-save title over a saved document");
                Assert.That(faults, Is.Not.Empty, "and the store's fault is still recorded");
            });
        }
    }

    /// <summary>A write that genuinely fails is still refused and still reported — the guard was narrowed, not removed.</summary>
    /// <remarks>
    /// It does not pin <c>project-save-failed</c>: an unwritable target is caught by the save's own pre-flight and
    /// refused as <c>save-target-unwritable</c>, which names the condition better. Pinning the catch-all here
    /// would assert that the more specific refusal never runs.
    /// </remarks>
    [Test]
    public async Task ASaveWhoseWriteBreaksIsStillReported()
    {
        (ShellHarness harness, _) = await StartedAsync();
        using (harness)
        {
            harness.Dialogs.SavePath = Directory.CreateDirectory(harness.TempPath("not-a-file.vis")).FullName;

            bool saved = await harness.Session.SaveAsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(saved, Is.False, "a write that genuinely failed does not answer success");
                Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "and the installer is told");
            });
        }
    }

    // ---------- Open ----------

    /// <summary>
    /// The project IS open, so nothing may say it could not be opened.
    /// </summary>
    [Test]
    public async Task AnOpenWhoseBookkeepingBreaksIsNotReportedAsAFailure()
    {
        (ShellHarness harness, List<InternalError> faults) = await StartedAsync();
        using (harness)
        {
            string path = await AProjectFileAsync(harness, "reopen.vis");
            harness.Dialogs.Reset();
            faults.Clear();
            harness.Session.StateChanged += (_, _) => throw Breaking();

            bool opened = await harness.Session.OpenAsync(path);

            Assert.Multiple(() =>
            {
                Assert.That(opened, Is.True, "the document is open; the answer must say so");
                // ANSWERING TRUE MEANS THERE IS A DOCUMENT. Adopting the project — creating or re-opening it and
                // building its index — is what makes the project open, so it stays inside the LOAD guard and out
                // of the contained bookkeeping: contained, a failure there would report success over a null
                // document, and StartAsync would skip the empty-project fallback that keeps the shell usable.
                Assert.That(harness.Session.Current, Is.Not.Null, "so a document exists");
                Assert.That(harness.Session.FilePath, Is.EqualTo(path), "and it is the one that was asked for");
                Assert.That(harness.Dialogs.LastProblem, Is.Null,
                    "nothing may tell the installer the open failed");
                Assert.That(faults, Is.Not.Empty, "and the real cause is recorded");
                Assert.That(faults[0].Detail, Does.Contain(Boom));
            });
        }
    }

    /// <summary>
    /// THE CONSEQUENCE THAT MATTERS. <c>StartAsync</c> falls through to the empty starter project when the open
    /// answers false, so a bookkeeping fault used to DISCARD a project that had loaded perfectly well — a
    /// double-clicked <c>.vis</c> replaced by an empty one, behind a dialog saying it could not be opened.
    /// </summary>
    [Test]
    public async Task StartupKeepsAProjectThatLoadedDespiteABrokenSubscriber()
    {
        (ShellHarness harness, _) = await StartedAsync();
        using (harness)
        {
            string path = await AProjectFileAsync(harness, "startup.vis");
            harness.Dialogs.Reset();
            harness.Session.StateChanged += (_, _) => throw Breaking();

            await harness.Session.StartAsync(path);

            Assert.That(harness.Session.FilePath, Is.EqualTo(path),
                "the loaded project must not be replaced by the empty starter project");
        }
    }

    /// <summary>A file that genuinely will not load is still refused — the narrowed guard still guards.</summary>
    [Test]
    public async Task AnOpenOfAMalformedFileStillFails()
    {
        (ShellHarness harness, _) = await StartedAsync();
        using (harness)
        {
            string rotten = harness.TempPath("rotten.vis");
            await File.WriteAllTextAsync(rotten, "this is not a project file");

            bool opened = await harness.Session.OpenAsync(rotten);

            Assert.Multiple(() =>
            {
                Assert.That(opened, Is.False);
                // The dialog carries the SDK's CAUSE code (load-not-xml), not the shell's framing code — the
                // one-child chain the open path builds. Asserting the framing code here would assert the
                // opposite of the contract, so what is pinned is that the installer was told at all.
                Assert.That(harness.Dialogs.LastProblem, Is.Not.Null);
            });
        }
    }

    // ---------- Catalog import ----------

    /// <summary>
    /// The definition WAS accepted and copied into the catalog folder, so calling the file invalid is false twice
    /// over — and the copy means a rejected-looking file is silently re-imported at every later start-up.
    /// </summary>
    [Test]
    public async Task AnImportWhoseNotificationBreaksIsNotReportedAsARejectedFile()
    {
        (ShellHarness harness, List<InternalError> faults) = await StartedAsync();
        using (harness)
        {
            harness.Session.CatalogChanged += (_, _) => throw Breaking();

            bool imported = await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: false);

            Assert.Multiple(() =>
            {
                Assert.That(imported, Is.True, "the definition registered; the answer must say so");
                Assert.That(harness.Dialogs.LastProblem, Is.Null,
                    "nothing may tell the installer the file was rejected");
                Assert.That(faults, Is.Not.Empty, "and the real cause is recorded");
                Assert.That(faults[0].Detail, Does.Contain(Boom));
            });
        }
    }

    /// <summary>A file that really is not a definition is still rejected, naming itself.</summary>
    [Test]
    public async Task AGenuinelyInvalidDefinitionIsStillRejected()
    {
        (ShellHarness harness, _) = await StartedAsync();
        using (harness)
        {
            string broken = harness.TempPath("broken.def");
            await File.WriteAllTextAsync(broken, "not a definition");

            bool imported = await harness.Session.ImportCatalogFileAsync(broken, persist: false);

            Assert.Multiple(() =>
            {
                Assert.That(imported, Is.False);
                // As above: the chain's CAUSE is what reaches the dialog (import-catalog-unparsable).
                Assert.That(harness.Dialogs.LastProblem, Is.Not.Null);
            });
        }
    }

    /// <summary>
    /// A folder import raises its notification from a <c>finally</c>. Unguarded, a throwing subscriber escaped the
    /// method entirely and took the <see cref="CatalogImportOutcome"/> with it — so N files that were imported AND
    /// persisted were reported as the shell's generic catch-all.
    /// </summary>
    [Test]
    public async Task AFolderImportWhoseNotificationBreaksStillReportsItsOutcome()
    {
        (ShellHarness harness, List<InternalError> faults) = await StartedAsync();
        using (harness)
        {
            string folder = Directory.CreateDirectory(harness.TempPath("defs")).FullName;
            File.Copy(SampleProductDef(), Path.Combine(folder, "a.def"));
            harness.Session.CatalogChanged += (_, _) => throw Breaking();

            CatalogImportOutcome outcome = await harness.Session.ImportCatalogFolderAsync(folder, persist: false);

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Imported, Is.EqualTo(1), "the outcome survives the broken notification");
                Assert.That(outcome.Completed, Is.True);
                Assert.That(faults, Is.Not.Empty, "and the fault is recorded rather than escaping");
            });
        }
    }
}
