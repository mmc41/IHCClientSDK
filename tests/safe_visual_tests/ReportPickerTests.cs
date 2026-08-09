using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>
/// T015 (R12/D4/D01, AC10): the three Documentation-menu report entries each open the ONE shared picker
/// dialog with their report pre-selected in the type dropdown; the commands ride the registry's
/// availability gate (disabled without an open project); and [Vis] generates the report via the facade in
/// the picked format to a temp file and hands it to the OS default application.
/// </summary>
public class ReportPickerTests
{
    private static readonly (string CommandId, ReportKind Expected)[] MenuEntries =
    {
        ("reports.functions", ReportKind.Functions),
        ("reports.installation", ReportKind.Installation),
        ("reports.functionBlocks", ReportKind.FunctionBlocks),
    };

    [AvaloniaTest]
    public async Task EachMenuEntry_OpensThePicker_WithItsReportPreselected()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);

        foreach ((string commandId, ReportKind expected) in MenuEntries)
        {
            await vm.Registry.Commands[commandId].ExecuteAsync(null);

            Assert.That(harness.Dialogs.LastReportPickerViewModel, Is.Not.Null,
                $"{commandId} opens the shared picker dialog");
            Assert.That(harness.Dialogs.LastReportPickerViewModel!.SelectedKind.Kind, Is.EqualTo(expected),
                $"{commandId} pre-selects its report in the type dropdown");
            Assert.That(harness.Dialogs.LastReportPickerViewModel!.SelectedFormat.MimeType,
                Is.EqualTo(ReportMimeTypes.Html),
                $"{commandId} opens the picker with HTML pre-selected in the format dropdown");
        }
        Assert.That(harness.Dialogs.ShowReportPickerCalls, Is.EqualTo(MenuEntries.Length));
    }

    [AvaloniaTest]
    public async Task ReportCommands_AreDisabledWithoutAProject_AndEnableWhenOneOpens()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();

        Assert.Multiple(() =>
        {
            foreach ((string commandId, _) in MenuEntries)
            {
                Assert.That(vm.Registry.Bar[commandId].Enabled, Is.False,
                    $"{commandId} is disabled while no project is open (the registry gate)");
            }
        });

        await vm.Registry.Commands["file.new"].ExecuteAsync(null);
        Assert.Multiple(() =>
        {
            foreach ((string commandId, _) in MenuEntries)
            {
                Assert.That(vm.Registry.Bar[commandId].Enabled, Is.True,
                    $"{commandId} enables once a project is open");
            }
        });
    }

    [AvaloniaTest]
    public async Task SaveAs_WritesTheFormatPickedInTheDropdown_WithTheFacadeBytes()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);

        foreach (string mimeType in new[] { ReportMimeTypes.Html, ReportMimeTypes.PlainText })
        {
            await vm.Registry.Commands["reports.functions"].ExecuteAsync(null);
            ReportPickerViewModel picker = harness.Dialogs.LastReportPickerViewModel!;
            picker.SelectedFormat = picker.Formats.Single(format => format.MimeType == mimeType);
            string extension = mimeType == ReportMimeTypes.PlainText ? ".txt" : ".html";
            string target = harness.TempPath("t016-report" + extension);
            harness.Dialogs.SaveReportPath = target;

            await picker.SaveAsCommand.ExecuteAsync(null);

            using var expected = new MemoryStream();
            await harness.ProjectService.GenerateReport(harness.Session.Current!, ReportKind.Functions,
                ReportMode.Standard, mimeType, expected,
                mimeType == ReportMimeTypes.Html ? new SvgReportIconProvider() : null);   // the app's icon mapping for HTML
            Assert.Multiple(() =>
            {
                Assert.That(harness.Dialogs.LastReportSuggestedName, Does.EndWith(extension),
                    $"the save dialog is suggested a {mimeType} file name");
                Assert.That(File.Exists(target), Is.True, "[Gem som…] writes the chosen file");
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(expected.ToArray()),
                    $"the picked {mimeType} format generates the facade's bytes for that format");
            });
        }
    }

    [AvaloniaTest]
    public async Task SaveFailure_SurfacesTheStandardErrorDialog()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);
        await vm.Registry.Commands["reports.functions"].ExecuteAsync(null);
        ReportPickerViewModel picker = harness.Dialogs.LastReportPickerViewModel!;
        harness.Dialogs.SaveReportPath = harness.TempPath(Path.Combine("no-such-dir", "report.html"));

        await picker.SaveAsCommand.ExecuteAsync(null);

        Assert.That(harness.Dialogs.LastMessage, Is.Not.Null.And.Not.Empty,
            "a save failure surfaces through the standard message dialog instead of throwing");
    }

    [AvaloniaTest]
    public async Task ViewInBrowser_GeneratesTheReportHtml_AndOpensItInTheBrowser()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);
        await vm.Registry.Commands["reports.installation"].ExecuteAsync(null);
        ReportPickerViewModel picker = harness.Dialogs.LastReportPickerViewModel!;

        picker.IsFullMode = true;
        await picker.ViewInBrowserCommand.ExecuteAsync(null);

        string? opened = harness.Dialogs.LastOpenedUrl;
        Assert.Multiple(() =>
        {
            Assert.That(opened, Does.EndWith(".html"), "the picker hands a generated HTML page to the browser");
            Assert.That(File.Exists(opened), Is.True, "the temp page exists on disk");
            Assert.That(File.ReadAllText(opened!),
                Does.Contain("<h1>Installationsdokumentation</h1>").And.Contain("Fuld rapport"),
                "the page is the facade-generated report for the picked kind and mode");
        });
    }

    [AvaloniaTest]
    public async Task ViewInBrowser_HonoursTheTxtFormat_AndOpensThePlainTextDocument()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);
        await vm.Registry.Commands["reports.installation"].ExecuteAsync(null);
        ReportPickerViewModel picker = harness.Dialogs.LastReportPickerViewModel!;

        picker.SelectedFormat = picker.Formats.Single(format => format.MimeType == ReportMimeTypes.PlainText);
        await picker.ViewInBrowserCommand.ExecuteAsync(null);

        string? opened = harness.Dialogs.LastOpenedUrl;
        Assert.Multiple(() =>
        {
            Assert.That(opened, Does.EndWith(".txt"), "the picked TXT format hands the browser a text document");
            Assert.That(File.ReadAllText(opened!),
                Does.Contain("Installationsdokumentation").And.Not.Contains("<h1>"),
                "the document is the facade-generated plain-text report, not the HTML page");
        });
    }

    /// <summary>
    /// The temp page's file NAME is deterministic (kind-mode.ext — it is what the viewer's tab shows), so a
    /// shared directory means two running instances viewing the same report write the same path: the second
    /// overwrites the page the first is reading, or fails outright while that viewer holds the file open. The
    /// directory is therefore scoped to the process — PIDs are unique among live processes, which is exactly and
    /// only what is needed here.
    /// <para>The isolation itself cannot be observed in-process: two harnesses in one test run share this
    /// process's id and so, correctly, share its directory. What is asserted is the scheme — that the page lands
    /// under a directory named for this process rather than directly in the shared root.</para>
    /// </summary>
    [AvaloniaTest]
    public async Task ViewInBrowser_ScopesTheTempPageToThisProcess_SoASecondInstanceCannotOverwriteIt()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);
        await vm.Registry.Commands["reports.installation"].ExecuteAsync(null);
        ReportPickerViewModel picker = harness.Dialogs.LastReportPickerViewModel!;

        await picker.ViewInBrowserCommand.ExecuteAsync(null);

        string opened = harness.Dialogs.LastOpenedUrl!;
        string? directory = Path.GetDirectoryName(opened);
        Assert.Multiple(() =>
        {
            Assert.That(Path.GetFileName(directory),
                Is.EqualTo(Environment.ProcessId.ToString(CultureInfo.InvariantCulture)),
                "the page goes in a directory named for THIS process, so a second running instance viewing the same report writes elsewhere");
            Assert.That(Path.GetFileName(Path.GetDirectoryName(directory)), Is.EqualTo("ihc-openvisual-reports"),
                "the per-process directory stays under the app's one reports root");
            Assert.That(Path.GetFileName(opened), Is.EqualTo("installation-standard.html"),
                "the file NAME stays deterministic — it is what the viewer shows the installer");
        });
    }
}
