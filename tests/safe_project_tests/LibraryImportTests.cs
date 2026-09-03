using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;

namespace Ihc.Vis.Tests;

/// <summary>E16 (US-059/060/061/062): importing product/function-block definition files from the Library — single
/// file, folder (recursive, counted), persistence across restart, and clear errors that name the offending file.</summary>
public class LibraryImportTests
{
    // The shared oracle fixtures (tests\testdata\), copied next to the test assembly by tests\TestData.props.
    private static string TestDataRoot() =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata");

    private static string SampleProductDef() =>
        Path.Combine(TestDataRoot(), "products", "synthetic", "synthetic_9f01_input.def");

    private static string SampleFunctionBlockIfb() =>
        Path.Combine(TestDataRoot(), "functionblocks", "synthetic", "synthetic_fb01_toggle.ifb");

    [Test]
    public async Task ImportProductFile_MakesItAvailableForInsertion()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.ProjectService.GetAvailableProducts().Count;

        var ok = await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.ProjectService.GetAvailableProducts().Count, Is.EqualTo(before + 1), "the imported product is available");
        });
    }

    [Test]
    public async Task ImportFunctionBlockFile_MakesItAvailableForInsertion()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.ProjectService.GetAvailableFunctionBlocks().Count;

        var ok = await harness.Session.ImportCatalogFileAsync(SampleFunctionBlockIfb(), persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.ProjectService.GetAvailableFunctionBlocks().Count, Is.EqualTo(before + 1));
        });
    }

    [Test]
    public async Task ImportFolder_ImportsRecursively_AndReportsCount()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int productsBefore = harness.ProjectService.GetAvailableProducts().Count;
        int blocksBefore = harness.ProjectService.GetAvailableFunctionBlocks().Count;
        var folder = harness.TempPath("import");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.Copy(SampleProductDef(), Path.Combine(folder, "a.def"));
        File.Copy(Path.Combine(TestDataRoot(), "products", "synthetic", "synthetic_9f02_output.def"), Path.Combine(folder, "sub", "b.def"));
        File.Copy(SampleFunctionBlockIfb(), Path.Combine(folder, "c.ifb"));

        CatalogImportOutcome outcome = await harness.Session.ImportCatalogFolderAsync(folder, persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Imported, Is.EqualTo(3), "the folder import reports the number of definition files (incl. subfolders)");
            Assert.That(outcome.Completed, Is.True, "and reports that it read the whole folder");
            Assert.That(harness.ProjectService.GetAvailableProducts().Count, Is.EqualTo(productsBefore + 2));
            Assert.That(harness.ProjectService.GetAvailableFunctionBlocks().Count, Is.EqualTo(blocksBefore + 1));
        });
    }

    [Test]
    public async Task ImportFolder_EmptyAndMissing_AreReported()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var empty = harness.TempPath("empty");
        Directory.CreateDirectory(empty);

        CatalogImportOutcome emptyOutcome = await harness.Session.ImportCatalogFolderAsync(empty, false);
        CatalogImportOutcome missing = await harness.Session.ImportCatalogFolderAsync(harness.TempPath("does-not-exist"), false);

        Assert.Multiple(() =>
        {
            Assert.That(emptyOutcome, Is.EqualTo(new CatalogImportOutcome(0, Stopped: false)),
                "an empty folder imports nothing — but it WAS read to the end");
            Assert.That(missing.FolderMissing, Is.True, "a missing folder is reported, not silently ignored");
            Assert.That(missing.Completed, Is.False, "and is never announced as a finished import");
        });
    }

    [Test]
    public async Task ImportMalformedFile_NamesTheFile_AndLeavesCatalogUnchanged()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.ProjectService.GetAvailableProducts().Count;
        var broken = harness.TempPath("broken.def");
        File.WriteAllText(broken, "this is not a valid catalog definition <<<");

        var ok = await harness.Session.ImportCatalogFileAsync(broken, persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            // US-062 names the file; D01 gives the sentence to the SDK's cause, so the two live in title and body.
            Assert.That(harness.Dialogs.LastMessageTitle, Does.Contain("broken.def"), "the error names the offending file");
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("katalogfil"), "and the body says why it was rejected");
            Assert.That(harness.ProjectService.GetAvailableProducts().Count, Is.EqualTo(before), "the available set is unchanged");
        });
    }

    [Test]
    public async Task ImportFolder_StopsAtFirstUnreadableFile_NamingIt()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int productsBefore = harness.ProjectService.GetAvailableProducts().Count;
        var folder = harness.TempPath("mixed");
        Directory.CreateDirectory(folder);
        File.Copy(SampleProductDef(), Path.Combine(folder, "1_good.def"));   // sorts first
        File.WriteAllText(Path.Combine(folder, "2_broken.def"), "garbage <<<");
        File.Copy(SampleFunctionBlockIfb(), Path.Combine(folder, "3_after.ifb"));

        CatalogImportOutcome outcome = await harness.Session.ImportCatalogFolderAsync(folder, persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Imported, Is.EqualTo(1), "the import stops at the first unreadable file, keeping earlier ones");
            Assert.That(outcome.Completed, Is.False, "and says it stopped — 1 imported here means something else than 1 of 1");
            // US-062 names the file; D01 gives the sentence to the SDK's cause, so the two live in title and body.
            Assert.That(harness.Dialogs.LastMessageTitle, Does.Contain("2_broken.def"), "the box names the offending file");
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("katalogfil"), "and the body says why it stopped there");
            Assert.That(harness.ProjectService.GetAvailableProducts().Count, Is.EqualTo(productsBefore + 1), "only the file before it imported");
        });
    }

    [Test]
    public async Task PersistedImport_IsAvailableAfterRestart_ButDeclinedIsNot()
    {
        // Persisted import survives a restart.
        using (var harness = ShellHarness.Create())
        {
            var vm = harness.CreateViewModel();
            await vm.InitializeAsync();
            int baseline = harness.ProjectService.GetAvailableProducts().Count;
            await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: true);

            using var restart = ShellHarness.Restart(harness.TempDir);
            Assert.That(restart.ProjectService.GetAvailableProducts().Count, Is.EqualTo(baseline + 1),
                "a persisted import loads from the app-data catalog folder on startup");
        }

        // A declined-persistence import is gone after a restart.
        using (var harness = ShellHarness.Create())
        {
            var vm = harness.CreateViewModel();
            await vm.InitializeAsync();
            int baseline = harness.ProjectService.GetAvailableProducts().Count;
            await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: false);

            using var restart = ShellHarness.Restart(harness.TempDir);
            Assert.That(restart.ProjectService.GetAvailableProducts().Count, Is.EqualTo(baseline),
                "an un-persisted import is absent after a restart");
        }
    }

    // M4 (T007): the startup persisted-catalog reload must import files in a DETERMINISTIC Ordinal path order, so
    // last-import-wins resolution and the resulting menu order are stable across machines/filesystems — not the raw
    // directory enumeration order (which on NTFS is case-INsensitive, and on other filesystems is unsorted). The
    // filenames below make the Ordinal order ("B…" < "a…", case-sensitive) differ from the raw NTFS order ("a…" <
    // "B…"), so before the fix the wrong file imports first and the two products land in the wrong relative order.
    [Test]
    public void PersistedCatalog_LoadsInDeterministicOrdinalOrder_RegardlessOfFilesystemOrder()
    {
        using var harness = ShellHarness.Create();
        var catalogDir = Path.Combine(harness.TempDir, "catalog");
        Directory.CreateDirectory(catalogDir);
        File.Copy(SampleProductDef(), Path.Combine(catalogDir, "B_first.def"));   // _0x9f01 (Ordinal-first)
        File.Copy(Path.Combine(TestDataRoot(), "products", "synthetic", "synthetic_9f02_output.def"),
                  Path.Combine(catalogDir, "a_second.def"));                       // _0x9f02

        using var restart = ShellHarness.Restart(harness.TempDir);
        var ids = restart.ProjectService.GetAvailableProducts().Select(p => p.ProductIdentifier).ToList();

        Assert.That(ids.IndexOf("_0x9f01"), Is.LessThan(ids.IndexOf("_0x9f02")),
            "the Ordinal-first file (B_first.def) imports first, so its product precedes the other — deterministic across filesystems");
    }

    /// <summary>
    /// REPRODUCE-FIRST for `sec 10.3` row 16: the start-up persisted-catalog load was best-effort AND unspanned,
    /// so a machine where every persisted file was skipped started identically to one where none were, and
    /// nothing afterwards could tell them apart.
    /// </summary>
    /// <remarks>
    /// The best-effort behaviour is deliberately UNCHANGED and is asserted here too: a rotted definition must not
    /// stop the application opening. What changed is that the pass is now measurable — the counts say what
    /// happened, and a skipped file does not make the span an error, because best-effort means it is not one.
    /// </remarks>
    [Test]
    public void ThePersistedLoadRecordsASpanCountingWhatItLoadedAndSkipped()
    {
        using Ihc.Tests.Shared.TelemetryCapture capture = Ihc.Tests.Shared.TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: "CatalogImportWorkflow.");
        using TraceProbe probe = TraceProbe.Start();
        using var harness = ShellHarness.Create();
        var catalogDir = Path.Combine(harness.TempDir, "catalog");
        Directory.CreateDirectory(catalogDir);
        File.Copy(SampleProductDef(), Path.Combine(catalogDir, "good.def"));
        File.WriteAllText(Path.Combine(catalogDir, "rotten.def"), "this is not a definition file");

        using var restart = ShellHarness.Restart(harness.TempDir);

        System.Diagnostics.Activity span = probe.Spans(capture)
            .Last(a => a.OperationName.EndsWith("LoadPersisted", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(span.GetTagItem(CatalogImportWorkflow.LoadedTag), Is.EqualTo(1));
            Assert.That(span.GetTagItem(CatalogImportWorkflow.SkippedTag), Is.EqualTo(1),
                "the pass says how many it went on without — which nothing could say before");
            Assert.That(span.Status, Is.EqualTo(System.Diagnostics.ActivityStatusCode.Unset),
                "a skipped file is not a failed pass: best-effort means exactly that");
            Assert.That(restart.ProjectService.GetAvailableProducts().Select(p => p.ProductIdentifier),
                Does.Contain("_0x9f01"),
                "and the good file still loaded — the best-effort behaviour is unchanged");
        });
    }

    // US-059/US-044: the Library menu command imports the picked file and refreshes the insertion menus.
    [Test]
    public async Task ImportCatalogFileCommand_ImportsPickedFile()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.ProjectService.GetAvailableProducts().Count;
        harness.Dialogs.CatalogFilePath = SampleProductDef();

        await vm.ImportCatalogFileCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.ProjectService.GetAvailableProducts().Count, Is.EqualTo(before + 1));
            Assert.That(vm.StatusText, Does.Contain("Importerede 1 komponent"));
        });
    }

    /// <summary>
    /// Rebuilding the insertion menus must not raise one collection notification PER ITEM. Each notification on a
    /// bound <c>ObservableCollection</c> costs a UI update, and both official Avalonia sources say the same thing:
    /// replace the collection instead of adding into it (performance review BP-22 / architecture AP-20). The
    /// product forest is catalog-sized, so a per-item rebuild is a burst of hundreds of updates for one import.
    /// <para>Asserted as a BOUND on notifications rather than an exact count, so a legitimate change of rebuild
    /// strategy does not break the test — only a return to per-item churn does.</para>
    /// </summary>
    [Test]
    public async Task RebuildingTheCatalogMenus_DoesNotNotifyPerItem()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int notifications = 0;
        vm.ProductsMenu.CollectionChanged += (_, _) => notifications++;
        int topLevelCategories = vm.ProductsMenu.Count;
        Assert.That(topLevelCategories, Is.GreaterThan(1), "precondition: the product menu has several categories");

        await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: false);   // raises CatalogChanged

        Assert.That(vm.ProductsMenu, Is.Not.Empty, "the menu is rebuilt, not emptied");
        Assert.That(notifications, Is.LessThanOrEqualTo(2),
            $"a rebuild should be a wholesale replacement (a clear plus a reset at worst), not {notifications} "
            + "notifications for a menu of " + vm.ProductsMenu.Count + " categories");
    }
}
