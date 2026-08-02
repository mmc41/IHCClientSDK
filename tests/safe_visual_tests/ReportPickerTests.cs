using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.NUnit;
using ihc_openvisual.ViewModels;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>
/// T015 (R12/D4/D01, AC10): the three Documentation-menu report entries each open the ONE shared picker
/// dialog with their report pre-selected in the type dropdown; the commands ride the registry's
/// availability gate (disabled without an open project); and [Vis i browser] generates the report via the
/// facade to a temp HTML file and hands it to the default browser.
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
    public async Task SaveAs_WritesTheChosenFormat_WithTheFacadeBytes()
    {
        using var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.Registry.Commands["file.new"].ExecuteAsync(null);
        await vm.Registry.Commands["reports.functions"].ExecuteAsync(null);
        ReportPickerViewModel picker = harness.Dialogs.LastReportPickerViewModel!;
        string target = harness.TempPath("t016-report.txt");
        harness.Dialogs.SaveReportPath = target;

        await picker.SaveAsCommand.ExecuteAsync(null);

        using var expected = new System.IO.MemoryStream();
        await harness.ProjectService.GenerateReport(harness.Session.Current!, ReportKind.Functions,
            ReportMode.Standard, ReportMimeTypes.PlainText, expected);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(target), Is.True, "[Gem som…] writes the chosen file");
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(expected.ToArray()),
                "a .txt target selects text/plain and the bytes are exactly the facade's");
        });
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
}
