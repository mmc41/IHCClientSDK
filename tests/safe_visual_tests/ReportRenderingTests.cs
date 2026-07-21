using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using Ihc.Vis;

namespace safe_visual_tests;

/// <summary>US-040: the model→HTML transform (mechanical, no business logic) and the browser-open flow. The
/// SDK owns field/order/omission/note-propagation; these tests verify the app renders the model 1-to-1 and
/// applies the print-variant differences.</summary>
public class ReportRenderingTests
{
    private static InstallationReport SampleInstallation() => new(
        "Installationsdokumentation",
        new ReportPartyInfo("Eve Installer", "2 High St", "333"),
        new ReportPartyInfo("Bob Customer", "1 Main St", "111"),
        ImmutableArray<ModuleRow>.Empty,
        ImmutableArray<ModuleRow>.Empty,
        ImmutableArray.Create(new ProductDetailTable(
            ReportProductKind.Dataline,
            ImmutableArray.Create(new ReportLabelValue("Lokalitet", "Living room"), new ReportLabelValue("Komponent", "Push")),
            ImmutableArray.Create(new ReportTerminalRow("1", "Indgang 1.1", "red")))),
        ImmutableArray<ProductDetailTable>.Empty,
        ImmutableArray<DatalineCrossReferenceRow>.Empty,
        ImmutableArray<DatalineCrossReferenceRow>.Empty,
        ImmutableArray<SpecialProductRow>.Empty,
        ImmutableArray<S0DeviceRow>.Empty);

    private static EndUserReport SampleEndUser() => new(
        "Funktionsdokumentation",
        ImmutableArray.Create(new EndUserLocality("Living room", "_0x2132",
            ImmutableArray.Create(new EndUserProduct("Push", "By door", "prod",
                ImmutableArray.Create(new EndUserTerminal("Left",
                    ImmutableArray.Create(new EndUserNote("Turns on the lamp", "Kitchen")))))))));

    [Test]
    public void Installation_Screen_RendersMastheadsAndProductDetail()
    {
        var html = ReportHtmlRenderer.RenderInstallation(SampleInstallation(), print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.StartWith("<!doctype html>"));
            Assert.That(html, Does.Contain("Installationsdokumentation"));
            Assert.That(html, Does.Contain("Installatør").And.Contain("Eve Installer").And.Contain("333"));
            Assert.That(html, Does.Contain("Kunde").And.Contain("Bob Customer"));
            Assert.That(html, Does.Contain("Living room").And.Contain("Push"), "the per-product detail table renders");
            Assert.That(html, Does.Contain("Indgang 1.1").And.Contain("red"), "the terminal sub-table renders");
        });
    }

    [Test]
    public void Installation_Print_UsesCompactPrintStylesheet()
    {
        var screen = ReportHtmlRenderer.RenderInstallation(SampleInstallation(), print: false);
        var print = ReportHtmlRenderer.RenderInstallation(SampleInstallation(), print: true);

        Assert.Multiple(() =>
        {
            Assert.That(print, Does.Contain("xx-small"), "the printer variant is compact");
            Assert.That(print, Does.Contain("page-break-inside:avoid"), "tables are kept whole across pages");
            Assert.That(screen, Does.Not.Contain("page-break-inside:avoid"), "the screen variant has no print rules");
            // Same content in both variants (installation report = CSS swap only).
            Assert.That(print, Does.Contain("Eve Installer").And.Contain("Push"));
        });
    }

    [Test]
    public void EndUser_Screen_HasTocAndDifferingLocalitySuffix()
    {
        var html = ReportHtmlRenderer.RenderEndUser(SampleEndUser(), print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("class=\"toc\"").And.Contain("href=\"#_0x2132\""), "the screen table-of-contents links localities");
            Assert.That(html, Does.Contain("Push By door"), "name + location");
            Assert.That(html, Does.Contain("Turns on the lamp (Kitchen)"), "the differing-FB-locality suffix shows on screen");
        });
    }

    [Test]
    public void EndUser_Print_DropsTocAndSuffix()
    {
        var html = ReportHtmlRenderer.RenderEndUser(SampleEndUser(), print: true);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("class=\"toc\""), "the printer variant drops the table-of-contents");
            Assert.That(html, Does.Contain("Turns on the lamp").And.Not.Contain("(Kitchen)"), "the print variant drops the locality suffix");
        });
    }

    private static FunctionBlockReport SampleFb() => new(
        "Functionsblok dokumentation",
        ImmutableArray.Create(
            new FunctionBlockReportEntry("Kip block", ImmutableArray.Create(
                new FunctionBlockReportSection("Input", ImmutableArray.Create("Toggle", "On")),
                new FunctionBlockReportSection("Output", ImmutableArray.Create("Lamp")))),
            new FunctionBlockReportEntry("PIR block", ImmutableArray.Create(
                new FunctionBlockReportSection("Input", ImmutableArray.Create("Motion"))))));

    // US-041: the FB report renders the heading and each block (in document order) with its sections and variables.
    [Test]
    public void FunctionBlocks_RendersBlocksInOrder_WithSections()
    {
        var html = ReportHtmlRenderer.RenderFunctionBlocks(SampleFb(), print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("<h2>Functionsblok dokumentation</h2>"));
            Assert.That(html.IndexOf("Kip block", StringComparison.Ordinal),
                Is.LessThan(html.IndexOf("PIR block", StringComparison.Ordinal)), "blocks render in document order");
            Assert.That(html, Does.Contain("Toggle").And.Contain("Lamp").And.Contain("Motion"), "each block's variables render");
            Assert.That(html, Does.Contain("Input").And.Contain("Output"), "section labels render");
        });
    }

    [Test]
    public void FunctionBlocks_Print_UsesCompactPrintStylesheet()
    {
        var print = ReportHtmlRenderer.RenderFunctionBlocks(SampleFb(), print: true);
        Assert.Multiple(() =>
        {
            Assert.That(print, Does.Contain("xx-small").And.Contain("page-break-inside:avoid"));
            Assert.That(print, Does.Contain("Kip block").And.Contain("PIR block"), "same content as screen (CSS swap)");
        });
    }

    // US-041: the command gathers the project's blocks and opens the report in the browser.
    [Test]
    public async Task FunctionBlockReportCommand_ListsBlocks_AndOpensBrowser()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);

        await vm.FunctionBlockReportScreenCommand.ExecuteAsync(null);

        var url = harness.Dialogs.LastOpenedUrl;
        Assert.Multiple(() =>
        {
            Assert.That(url, Is.Not.Null.And.StartWith("file:"));
            var text = File.ReadAllText(new Uri(url!).LocalPath);
            Assert.That(text, Does.Contain("Functionsblok dokumentation"));
            Assert.That(text, Does.Contain(ProjectWorkflow.EmptyBlockName), "the inserted block is listed");
            Assert.That(vm.StatusText, Is.EqualTo("Function-block report opened in your browser."));
        });
    }

    // Not part of the suite (Explicit) — writes representative report HTML to a path for visual inspection.
    [Test, Explicit]
    public void DumpSampleReports()
    {
        var dir = Environment.GetEnvironmentVariable("REPORT_DUMP_DIR") ?? Path.GetTempPath();
        File.WriteAllText(Path.Combine(dir, "sample-installation.html"), ReportHtmlRenderer.RenderInstallation(SampleInstallation(), false));
        File.WriteAllText(Path.Combine(dir, "sample-installation-print.html"), ReportHtmlRenderer.RenderInstallation(SampleInstallation(), true));
        File.WriteAllText(Path.Combine(dir, "sample-enduser.html"), ReportHtmlRenderer.RenderEndUser(SampleEndUser(), false));
        File.WriteAllText(Path.Combine(dir, "sample-functionblocks.html"), ReportHtmlRenderer.RenderFunctionBlocks(SampleFb(), false));
    }

    [Test]
    public async Task InstallationReportCommand_WritesHtml_AndOpensInBrowser()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.UpdateProjectInfoAsync(
            ProjectInfoData.Empty with { Installer = new ContactInfo("Eve", "", "", "", "", "", "", "") });

        await vm.InstallationReportScreenCommand.ExecuteAsync(null);

        var url = harness.Dialogs.LastOpenedUrl;
        Assert.Multiple(() =>
        {
            Assert.That(url, Is.Not.Null.And.StartWith("file:"), "the report opens in the standard browser");
            var path = new Uri(url!).LocalPath;
            Assert.That(File.Exists(path), Is.True, "the report HTML file is written");
            Assert.That(File.ReadAllText(path), Does.Contain("Installationsdokumentation").And.Contain("Eve"),
                "the entered installer info reaches the rendered report");
            Assert.That(vm.StatusText, Is.EqualTo("Installation report opened in your browser."));
        });
    }

    // T014 characterization (M6/C guardrail before the reports collaborator extraction): the EndUser report path
    // through ProjectWorkflow.GenerateEndUserReport (the wrapper T019 moves) via the VM command had NO workflow/VM
    // test — only the SDK generator and the HTML renderer were covered independently. Drive the whole wrapper.
    [Test]
    public async Task EndUserReportCommand_WritesHtml_AndOpensInBrowser()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.EndUserReportScreenCommand.ExecuteAsync(null);

        var url = harness.Dialogs.LastOpenedUrl;
        Assert.Multiple(() =>
        {
            Assert.That(url, Is.Not.Null.And.StartWith("file:"), "the end-user report opens in the standard browser");
            var path = new Uri(url!).LocalPath;
            Assert.That(File.Exists(path), Is.True, "the report HTML file is written");
            Assert.That(File.ReadAllText(path), Does.Contain("Funktionsdokumentation"),
                "the ProjectWorkflow.GenerateEndUserReport wrapper produced the rendered end-user report");
            Assert.That(vm.StatusText, Is.EqualTo("End-user report opened in your browser."));
        });
    }
}
