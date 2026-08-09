using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>
/// UX review CORE-02 / CORE-03: the outcomes the shell used to keep to itself. Each of these workflows ENDED in a
/// state the user (and any automation client reading the status line) could not tell apart from success — a report
/// that never opened, a folder import that stopped halfway. Logging is not reporting: nothing on screen changed, so
/// the answer to "did that work?" was "yes" in every case.
/// </summary>
public class OutcomeReportingTests
{
    // A report the OS could not open is not a report the user got.
    [Test]
    public async Task ReportThatCannotBeOpened_IsReportedInsteadOfLookingLikeSuccess()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        harness.Dialogs.OpenExternalUrlSucceeds = false;

        await harness.Session.ViewReportInBrowserAsync(ReportKind.Installation, ReportMode.Standard, "text/html");

        Assert.That(harness.Dialogs.LastMessage, Is.Not.Null.And.Contains("ikke åbnes"),
            "the failed handover is reported, and the message says where the generated file is");
    }

    // A folder import that stops at a bad file must not announce the same sentence as one that read everything.
    [Test]
    public async Task FolderImportThatStopsEarly_IsAnnouncedAsStoppedNotAsComplete()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        string good = Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "products");
        string dir = harness.TempPath("import");
        Directory.CreateDirectory(dir);
        foreach (string def in Directory.EnumerateFiles(good, "*.def"))
            File.Copy(def, Path.Combine(dir, Path.GetFileName(def)));
        // Ordinal-first so at least one real definition imports before the corrupt one aborts the run.
        File.WriteAllText(Path.Combine(dir, "zzz-broken.def"), "this is not a product definition");

        harness.Dialogs.CatalogFolderPath = dir;
        await vm.ImportCatalogFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Does.Contain("stoppet"),
                $"a half-finished import says so — status was '{vm.StatusText}'");
            Assert.That(vm.StatusText, Does.Not.Contain("Importerede"),
                "and does not use the wording a complete import uses");
        });
    }
}
