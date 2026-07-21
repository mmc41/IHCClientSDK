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

    private static InstallationReport FlatTablesInstallation() => new(
        "Installationsdokumentation",
        new ReportPartyInfo("--", "--", "--"),
        new ReportPartyInfo("--", "--", "--"),
        ImmutableArray.Create(new ModuleRow("1", "Input 8", "Hall", "Main input")),
        ImmutableArray.Create(new ModuleRow("2", "Output 8", "Hall", "Main output")),
        ImmutableArray<ProductDetailTable>.Empty,
        ImmutableArray<ProductDetailTable>.Empty,
        ImmutableArray.Create(new DatalineCrossReferenceRow(
            "1.1", "Push", "T1", "note", "Hall", "door", "K1", "5x0,75", "12", "L1", "red")),
        ImmutableArray<DatalineCrossReferenceRow>.Empty,
        ImmutableArray.Create(new SpecialProductRow("Modem", "T2", "n", "Hall", "shelf", "K2", "wh", "bk", "gn", "ye")),
        ImmutableArray.Create(new S0DeviceRow("Meter", "n2", "Garage", "wall", "K3", "bl", "br")));

    // Characterization of the four flat installation tables (modules, cross-references, special products, S0):
    // heading + header row + one cell row per item, and an empty section renders nothing at all.
    [Test]
    public void Installation_FlatTables_RenderHeadingsHeadersAndCells()
    {
        var html = ReportHtmlRenderer.RenderInstallation(FlatTablesInstallation(), print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain(
                "<h3>Datalinie input-moduler</h3><table><tr><th>Datalinie</th><th>Modultype</th><th>Lokalitet</th><th>Beskrivelse</th></tr>"));
            Assert.That(html, Does.Contain("<tr><td>1</td><td>Input 8</td><td>Hall</td><td>Main input</td></tr>"));
            Assert.That(html, Does.Contain("<h3>Datalinie output-moduler</h3>"));
            Assert.That(html, Does.Contain("<tr><td>2</td><td>Output 8</td><td>Hall</td><td>Main output</td></tr>"));
            Assert.That(html, Does.Contain(
                "<h3>Datalinie indgange</h3><table><tr><th>Adresse</th><th>Produkt</th><th>Terminal</th><th>Note</th>"
                + "<th>Lokalitet</th><th>Placering</th><th>Id-kode</th><th>Kabeltype</th><th>Kabelnummer</th>"
                + "<th>Lysgruppe</th><th>Ledningsfarve</th></tr>"));
            Assert.That(html, Does.Contain(
                "<tr><td>1.1</td><td>Push</td><td>T1</td><td>note</td><td>Hall</td><td>door</td><td>K1</td>"
                + "<td>5x0,75</td><td>12</td><td>L1</td><td>red</td></tr>"));
            Assert.That(html, Does.Not.Contain("Datalinie udgange"), "an empty section renders no heading/table");
            Assert.That(html, Does.Contain(
                "<h3>Specielle Produkter</h3><table><tr><th>Produkt</th><th>Terminal</th><th>Note</th><th>Lokalitet</th>"
                + "<th>Placering</th><th>Id-kode</th><th>0V</th><th>24V</th><th>RS485-</th><th>RS485+</th></tr>"));
            Assert.That(html, Does.Contain(
                "<tr><td>Modem</td><td>T2</td><td>n</td><td>Hall</td><td>shelf</td><td>K2</td>"
                + "<td>wh</td><td>bk</td><td>gn</td><td>ye</td></tr>"));
            Assert.That(html, Does.Contain(
                "<h3>S0 Device</h3><table><tr><th>Produkt</th><th>Note</th><th>Lokalitet</th><th>Placering</th>"
                + "<th>Id-kode</th><th>S0-</th><th>S0+</th></tr>"));
            Assert.That(html, Does.Contain(
                "<tr><td>Meter</td><td>n2</td><td>Garage</td><td>wall</td><td>K3</td><td>bl</td><td>br</td></tr>"));
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
