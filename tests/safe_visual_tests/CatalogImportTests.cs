using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace safe_visual_tests;

/// <summary>E16 (US-059/060/061/062): importing product/function-block definition files from the Library — single
/// file, folder (recursive, counted), persistence across restart, and clear errors that name the offending file.</summary>
public class CatalogImportTests
{
    // Walk up to the repo's shared test data (the byte-fidelity synthetic .def/.ifb oracles).
    private static string TestDataRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "safe_project_tests", "testdata")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "safe_project_tests", "testdata");
    }

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
        int before = harness.Session.GetAvailableProducts().Count;

        var ok = await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.GetAvailableProducts().Count, Is.EqualTo(before + 1), "the imported product is available");
        });
    }

    [Test]
    public async Task ImportFunctionBlockFile_MakesItAvailableForInsertion()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.GetAvailableFunctionBlocks().Count;

        var ok = await harness.Session.ImportCatalogFileAsync(SampleFunctionBlockIfb(), persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(harness.Session.GetAvailableFunctionBlocks().Count, Is.EqualTo(before + 1));
        });
    }

    [Test]
    public async Task ImportFolder_ImportsRecursively_AndReportsCount()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int productsBefore = harness.Session.GetAvailableProducts().Count;
        int blocksBefore = harness.Session.GetAvailableFunctionBlocks().Count;
        var folder = harness.TempPath("import");
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.Copy(SampleProductDef(), Path.Combine(folder, "a.def"));
        File.Copy(Path.Combine(TestDataRoot(), "products", "synthetic", "synthetic_9f02_output.def"), Path.Combine(folder, "sub", "b.def"));
        File.Copy(SampleFunctionBlockIfb(), Path.Combine(folder, "c.ifb"));

        int count = await harness.Session.ImportCatalogFolderAsync(folder, persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(3), "the folder import reports the number of definition files (incl. subfolders)");
            Assert.That(harness.Session.GetAvailableProducts().Count, Is.EqualTo(productsBefore + 2));
            Assert.That(harness.Session.GetAvailableFunctionBlocks().Count, Is.EqualTo(blocksBefore + 1));
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

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.ImportCatalogFolderAsync(empty, false).Result, Is.EqualTo(0), "an empty folder imports nothing");
            Assert.That(harness.Session.ImportCatalogFolderAsync(harness.TempPath("does-not-exist"), false).Result, Is.EqualTo(-1),
                "a missing folder is reported, not silently ignored");
        });
    }

    [Test]
    public async Task ImportMalformedFile_NamesTheFile_AndLeavesCatalogUnchanged()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.GetAvailableProducts().Count;
        var broken = harness.TempPath("broken.def");
        File.WriteAllText(broken, "this is not a valid catalog definition <<<");

        var ok = await harness.Session.ImportCatalogFileAsync(broken, persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("broken.def"), "the error names the offending file");
            Assert.That(harness.Session.GetAvailableProducts().Count, Is.EqualTo(before), "the available set is unchanged");
        });
    }

    [Test]
    public async Task ImportFolder_StopsAtFirstUnreadableFile_NamingIt()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int productsBefore = harness.Session.GetAvailableProducts().Count;
        var folder = harness.TempPath("mixed");
        Directory.CreateDirectory(folder);
        File.Copy(SampleProductDef(), Path.Combine(folder, "1_good.def"));   // sorts first
        File.WriteAllText(Path.Combine(folder, "2_broken.def"), "garbage <<<");
        File.Copy(SampleFunctionBlockIfb(), Path.Combine(folder, "3_after.ifb"));

        int count = await harness.Session.ImportCatalogFolderAsync(folder, persist: false);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1), "the import stops at the first unreadable file, keeping earlier ones");
            Assert.That(harness.Dialogs.LastMessage, Does.Contain("2_broken.def"), "the message names the offending file");
            Assert.That(harness.Session.GetAvailableProducts().Count, Is.EqualTo(productsBefore + 1), "only the file before it imported");
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
            int baseline = harness.Session.GetAvailableProducts().Count;
            await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: true);

            using var restart = ShellHarness.Restart(harness.TempDir);
            Assert.That(restart.Session.GetAvailableProducts().Count, Is.EqualTo(baseline + 1),
                "a persisted import loads from the app-data catalog folder on startup");
        }

        // A declined-persistence import is gone after a restart.
        using (var harness = ShellHarness.Create())
        {
            var vm = harness.CreateViewModel();
            await vm.InitializeAsync();
            int baseline = harness.Session.GetAvailableProducts().Count;
            await harness.Session.ImportCatalogFileAsync(SampleProductDef(), persist: false);

            using var restart = ShellHarness.Restart(harness.TempDir);
            Assert.That(restart.Session.GetAvailableProducts().Count, Is.EqualTo(baseline),
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
        var ids = restart.Session.GetAvailableProducts().Select(p => p.ProductIdentifier).ToList();

        Assert.That(ids.IndexOf("_0x9f01"), Is.LessThan(ids.IndexOf("_0x9f02")),
            "the Ordinal-first file (B_first.def) imports first, so its product precedes the other — deterministic across filesystems");
    }

    // US-059/US-044: the Library menu command imports the picked file and refreshes the insertion menus.
    [Test]
    public async Task ImportCatalogFileCommand_ImportsPickedFile()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.GetAvailableProducts().Count;
        harness.Dialogs.CatalogFilePath = SampleProductDef();

        await vm.ImportCatalogFileCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.GetAvailableProducts().Count, Is.EqualTo(before + 1));
            Assert.That(vm.StatusText, Does.Contain("Imported 1 component"));
        });
    }
}
