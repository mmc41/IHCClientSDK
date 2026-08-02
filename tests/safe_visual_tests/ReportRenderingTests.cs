using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.Services;
using ihc_openvisual.Services.Reporting;
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
            ImmutableArray.Create(new EndUserProduct("Push", "By door", "Tryk 4-tryk",
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

    // T031 / D16: a product is identified by its resolved catalog TYPE-NAME text (image-free), beside its name/placement
    // — never by a product image key.
    [Test]
    public void EndUser_RendersImageFreeProductTypeName()
    {
        var html = ReportHtmlRenderer.RenderEndUser(SampleEndUser(), print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("class=\"product-type\"").And.Contain("Tryk 4-tryk"),
                "the product type name renders as image-free identity text");
            Assert.That(html, Does.Contain("Push By door"), "the name/placement text renders beside the type name");
            Assert.That(html, Does.Not.Contain("<img"), "no product image is emitted");
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

    // US-041 / T021: the single Reports command opens the Reports view over the combined document, which lists the blocks.
    [Test]
    public async Task Reports_OpenReports_CombinedDocument_ListsBlocks()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);

        await vm.OpenReportsCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ShowReportsCalls, Is.EqualTo(1), "the Reports view opened");
            Assert.That(harness.Dialogs.LastReportsViewModel, Is.Not.Null);
            Assert.That(harness.Dialogs.LastReportsViewModel!.Html,
                Does.Contain("Functionsblok dokumentation").And.Contain(ProjectWorkflow.EmptyBlockName),
                "the combined document lists the inserted block");
        });
    }

    // Not part of the suite (Explicit) — writes representative report HTML to a path for visual inspection.
    [Test]
    [Explicit("Developer tool: writes sample HTML for manual visual inspection; not CI regression coverage.")]
    [Category("Tool")]
    public void DumpSampleReports()
    {
        var dir = Environment.GetEnvironmentVariable("REPORT_DUMP_DIR") ?? Path.GetTempPath();
        File.WriteAllText(Path.Combine(dir, "sample-installation.html"), ReportHtmlRenderer.RenderInstallation(SampleInstallation(), false));
        File.WriteAllText(Path.Combine(dir, "sample-installation-print.html"), ReportHtmlRenderer.RenderInstallation(SampleInstallation(), true));
        File.WriteAllText(Path.Combine(dir, "sample-enduser.html"), ReportHtmlRenderer.RenderEndUser(SampleEndUser(), false));
        File.WriteAllText(Path.Combine(dir, "sample-functionblocks.html"), ReportHtmlRenderer.RenderFunctionBlocks(SampleFb(), false));
    }

    // T021: the combined document composes the installation section — the entered installer info reaches it.
    [Test]
    public async Task Reports_CombinedDocument_ContainsInstallationContent()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.UpdateProjectInfoAsync(
            ProjectInfoData.Empty with { Installer = new ContactInfo("Eve", "", "", "", "", "", "", "") });

        await vm.OpenReportsCommand.ExecuteAsync(null);

        Assert.That(harness.Dialogs.LastReportsViewModel!.Html, Does.Contain("Installationsdokumentation").And.Contain("Eve"),
            "the entered installer info reaches the combined document's installation section");
    }

    // T021: the screen variant carries the report navigation; the printer variant drops it; one document composes all
    // three sections; and the six old report commands are gone.
    [Test]
    public async Task Reports_ScreenHasNavigation_PrintDropsIt_AndSixOldCommandsGone()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await vm.OpenReportsCommand.ExecuteAsync(null);
        var reports = harness.Dialogs.LastReportsViewModel!;

        reports.IsPrint = false;
        string screen = reports.Html;
        reports.IsPrint = true;
        string print = reports.Html;

        Assert.Multiple(() =>
        {
            Assert.That(screen, Does.Contain("toc overview").And.Contain("#section-installation").And.Contain("back-to-top"),
                "the on-screen variant is one navigable document (overview / section-jump / back-to-top)");
            Assert.That(print, Does.Not.Contain("overview").And.Not.Contain("back-to-top"),
                "the printer variant drops the on-screen navigation");
            Assert.That(screen, Does.Contain("Installationsdokumentation").And.Contain("Funktionsdokumentation").And.Contain("Functionsblok dokumentation"),
                "one document composes all three sections");
            foreach (string name in new[] { "InstallationReportScreenCommand", "InstallationReportPrintCommand",
                "EndUserReportScreenCommand", "EndUserReportPrintCommand", "FunctionBlockReportScreenCommand", "FunctionBlockReportPrintCommand" })
                Assert.That(typeof(ihc_openvisual.ViewModels.MainWindowViewModel).GetProperty(name), Is.Null, $"{name} was removed");
        });
    }

    // T021: the Reports view's Open-in-browser writes the combined HTML to a temp file and opens it in the browser.
    [Test]
    public async Task Reports_OpenInBrowser_WritesCombinedHtml_AndOpens()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await vm.OpenReportsCommand.ExecuteAsync(null);

        await harness.Dialogs.LastReportsViewModel!.OpenInBrowserCommand.ExecuteAsync(null);

        var url = harness.Dialogs.LastOpenedUrl;
        Assert.Multiple(() =>
        {
            Assert.That(url, Is.Not.Null.And.StartWith("file:"), "the combined report opens in the standard browser");
            Assert.That(File.ReadAllText(new Uri(url!).LocalPath), Does.Contain("Projektdokumentation"),
                "the written HTML is the combined project-documentation document");
        });
    }

    // T022: with a fixed injected clock, the rendered heading metadata shows the report GENERATION timestamp (in the
    // fixed yyyy-MM-dd HH:mm format) and the programmer — from the project info, not only the Projekt section.
    [Test]
    public async Task ReportHeading_ShowsGenerationTimestampAndProgrammer_FromInjectedClock()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 7, 21, 14, 30, 0, TimeSpan.Zero));
        using var harness = ShellHarness.Create(timeProvider: clock);
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.UpdateProjectInfoAsync(ProjectInfoData.Empty with { Programmer = "Ada" });

        await vm.OpenReportsCommand.ExecuteAsync(null);
        string html = harness.Dialogs.LastReportsViewModel!.Html;

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("report-meta"), "the timestamp + programmer live in the heading metadata line");
            Assert.That(html, Does.Contain("2026-07-21 14:30"), "the heading shows the generation time in the fixed format");
            Assert.That(html, Does.Contain("Ada"), "the heading shows the programmer");
        });
    }

    // T023 / US-039: the Projekt identity section (description / number / programmer) renders near the top.
    [Test]
    public async Task ProjektSection_RendersDescriptionNumberProgrammer()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.UpdateProjectInfoAsync(
            ProjectInfoData.Empty with { Description = "Villa", Number = "P-42", Programmer = "Ada" });

        await vm.OpenReportsCommand.ExecuteAsync(null);
        string html = harness.Dialogs.LastReportsViewModel!.Html;

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("class=\"projekt\"").And.Contain("Projekt"), "the Projekt section is rendered");
            Assert.That(html, Does.Contain("Beskrivelse").And.Contain("Villa"), "the description");
            Assert.That(html, Does.Contain("Nummer").And.Contain("P-42"), "the number");
            Assert.That(html, Does.Contain("Programmør").And.Contain("Ada"), "the programmer");
        });
    }

    // A minimal combined report over the sample sub-reports, with the given terminal details, cabling rows and map.
    private static ProjectDocumentationReport SampleCombined(
        ImmutableArray<ReportTerminalDetail> terminals, ImmutableArray<ReportKablerRow> kabler,
        ModuleAddressMap? moduleMap = null, ImmutableArray<ReportCompletenessRow> completeness = default,
        ImmutableArray<ReportFbBlock> functionBlocks = default) => new(
        new ReportValue("Project", "Project"),
        new ReportProjektInfo(new ReportValue("d", "d"), new ReportValue("n", "n"), new ReportValue("Ada", "Ada")),
        "2026-01-01 00:00",
        ImmutableArray.Create(
            new ReportSectionEntry("s-installation", ReportSectionKind.Installation, "Installationsdokumentation", true),
            new ReportSectionEntry("s-enduser", ReportSectionKind.EndUser, "Funktionsdokumentation", true),
            new ReportSectionEntry("s-fb", ReportSectionKind.FunctionBlock, "Functionsblok dokumentation", true)),
        ImmutableArray<ReportLocality>.Empty,
        ImmutableArray<ReportElementRef>.Empty,
        terminals,
        kabler,
        moduleMap ?? ModuleAddressMap.Empty,
        completeness.IsDefault ? ImmutableArray<ReportCompletenessRow>.Empty : completeness,
        functionBlocks.IsDefault ? ImmutableArray<ReportFbBlock>.Empty : functionBlocks,
        SampleInstallation(), SampleEndUser(), SampleFb());

    // T024: the technical terminal-connections table renders the link-display path and the function note.
    [Test]
    public void TerminalDetail_Render_ShowsConnectionsTableWithLinkPathAndNote()
    {
        var report = SampleCombined(ImmutableArray.Create(
            new ReportTerminalDetail("Tryk 1", "Knap A", "-> Tænd -> Stue-blok -> Stue", new ReportValue("Tænder lyset", "Tænder lyset"))),
            ImmutableArray<ReportKablerRow>.Empty);

        string html = ReportHtmlRenderer.RenderProjectDocumentation(report, print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Terminal-forbindelser"), "the terminal-connections table renders");
            Assert.That(html, Does.Contain("-&gt; Tænd -&gt; Stue-blok -&gt; Stue"), "the link-display path (arrows HTML-escaped)");
            Assert.That(html, Does.Contain("Tænder lyset"), "the function note");
        });
    }

    // T025: the consolidated Kabler cabling table renders its ten columns and a row's values.
    [Test]
    public void Kabler_Render_ShowsCablingTableWithColumns()
    {
        var report = SampleCombined(ImmutableArray<ReportTerminalDetail>.Empty, ImmutableArray.Create(
            new ReportKablerRow("Grøn", "1.02", "1", "Tavle", "Gruppe A", "ID9", "Stue", "Ved dør", "Tryk", "Indgang")));

        string html = ReportHtmlRenderer.RenderProjectDocumentation(report, print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Kabler").And.Contain("Ledningsfarve").And.Contain("Ind-Udgang"), "the Kabler table + its columns render");
            Assert.That(html, Does.Contain("Grøn").And.Contain("1.02").And.Contain("Gruppe A").And.Contain("Indgang"), "a cabling row's values render");
        });
    }

    // T026: the per-terminal module address map renders which product terminal occupies each address, per direction.
    [Test]
    public void ModuleMap_Render_ShowsPerTerminalOccupancy()
    {
        var map = new ModuleAddressMap(
            ImmutableArray.Create(new ModuleAddressEntry("1.01", "Tryk", "Knap A")),
            ImmutableArray.Create(new ModuleAddressEntry("2.03", "Relæ", "Ud 1")));
        var report = SampleCombined(ImmutableArray<ReportTerminalDetail>.Empty, ImmutableArray<ReportKablerRow>.Empty, map);

        string html = ReportHtmlRenderer.RenderProjectDocumentation(report, print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Input-modul adressekort").And.Contain("Output-modul adressekort"), "both module-map tables render");
            Assert.That(html, Does.Contain("1.01").And.Contain("Knap A"), "an input occupancy row");
            Assert.That(html, Does.Contain("2.03").And.Contain("Ud 1"), "an output occupancy row");
        });
    }

    // T027: the documentation-completeness section renders the issues; a clean report renders "none found".
    [Test]
    public void Completeness_Render_ShowsIssuesOrNoneFound()
    {
        var withIssue = SampleCombined(ImmutableArray<ReportTerminalDetail>.Empty, ImmutableArray<ReportKablerRow>.Empty,
            completeness: ImmutableArray.Create(new ReportCompletenessRow("Stue", "Tryk", "Knap A", "Ikke forbundet")));
        var clean = SampleCombined(ImmutableArray<ReportTerminalDetail>.Empty, ImmutableArray<ReportKablerRow>.Empty);

        string issueHtml = ReportHtmlRenderer.RenderProjectDocumentation(withIssue, print: false);
        string cleanHtml = ReportHtmlRenderer.RenderProjectDocumentation(clean, print: false);

        Assert.Multiple(() =>
        {
            Assert.That(issueHtml, Does.Contain("Fejl i dokumentation").And.Contain("Ikke forbundet").And.Contain("Knap A"), "issues render grouped by locality/product/terminal");
            Assert.That(cleanHtml, Does.Contain("Fejl i dokumentation").And.Contain("Ingen fejl fundet"), "a clean project renders none-found");
        });
    }

    // T028: the deep function-block layout renders description, notes, name=value variables, the program outline, and
    // "Tom blok" for an unprogrammed block.
    [Test]
    public void FunctionBlockReport_Deep_RendersLayoutAndTomBlok()
    {
        var programmed = new ReportFbBlock("Kip", "Anvendelse: kip tænd/sluk",
            ImmutableArray.Create(new ReportFbPin("Tænd", "Tænder udgang")),
            ImmutableArray.Create(new ReportFbPin("Udgang", "Til lampe")),
            ImmutableArray.Create(new ReportFbVariable("Timer", "0:3:0")),
            ImmutableArray.Create(new ReportFbVariable("Puls", "0:0:1")),
            ImmutableArray.Create("Program: Kip", "  Hændelse: %P -> ON", "    Kommando: %P = ON"), IsEmpty: false);
        var empty = new ReportFbBlock("Ny blok", string.Empty,
            ImmutableArray<ReportFbPin>.Empty, ImmutableArray<ReportFbPin>.Empty,
            ImmutableArray<ReportFbVariable>.Empty, ImmutableArray<ReportFbVariable>.Empty,
            ImmutableArray<string>.Empty, IsEmpty: true);
        var report = SampleCombined(ImmutableArray<ReportTerminalDetail>.Empty, ImmutableArray<ReportKablerRow>.Empty,
            functionBlocks: ImmutableArray.Create(programmed, empty));

        string html = ReportHtmlRenderer.RenderProjectDocumentation(report, print: false);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Anvendelse: kip tænd/sluk"), "the block description renders");
            Assert.That(html, Does.Contain("Tænder udgang").And.Contain("Til lampe"), "input and output notes render");
            Assert.That(html, Does.Contain("Timer = 0:3:0"), "a setting renders as name = value");
            Assert.That(html, Does.Contain("Kommando: %P = ON"), "the program outline renders");
            Assert.That(html, Does.Contain("Tom blok"), "an unprogrammed block renders as Tom blok");
        });
    }

    // A report exercising every switch: 3 sections, a blank Projekt field, an element id, a linked terminal with a
    // note, and a wire-coloured cabling row.
    private static ProjectDocumentationReport SwitchTestReport() => new(
        new ReportValue("Project", "Project"),
        new ReportProjektInfo(new ReportValue("Villa", "Villa"), new ReportValue(string.Empty, "--"), new ReportValue("Ada", "Ada")),
        "2026-01-01 00:00",
        ImmutableArray.Create(
            new ReportSectionEntry("s-installation", ReportSectionKind.Installation, "Installationsdokumentation", true),
            new ReportSectionEntry("s-enduser", ReportSectionKind.EndUser, "Funktionsdokumentation", true),
            new ReportSectionEntry("s-fb", ReportSectionKind.FunctionBlock, "Functionsblok dokumentation", true)),
        ImmutableArray<ReportLocality>.Empty,
        ImmutableArray.Create(new ReportElementRef("Tryk", "_0xABC", ReportSectionKind.Installation, true)),
        ImmutableArray.Create(new ReportTerminalDetail("Tryk 1", "Knap A", "-> Tænd -> Stue-blok -> Stue", new ReportValue("Tænder lyset", "Tænder lyset"))),
        ImmutableArray.Create(new ReportKablerRow("Grøn", "1.02", "1", "Tavle", "Gruppe A", "ID9", "Stue", "Ved dør", "Tryk", "Indgang")),
        ModuleAddressMap.Empty,
        ImmutableArray<ReportCompletenessRow>.Empty,
        ImmutableArray<ReportFbBlock>.Empty,
        SampleInstallation(), SampleEndUser(), SampleFb());

    // T029 / US-071: the Reports view-model's switches toggle each content section and detail option; an off section
    // emits nothing.
    [Test]
    public void ReportSwitch_TogglesEachSectionAndDetailOption()
    {
        var vm = new ihc_openvisual.ViewModels.ReportsViewModel(SwitchTestReport(),
            (_, _) => Task.FromResult<string?>(null), _ => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            // Content-section switches: off emits nothing (the heading is absent, not empty).
            Assert.That(vm.Html, Does.Contain("Installationsdokumentation"), "installation on by default");
            vm.ShowInstallation = false;
            Assert.That(vm.Html, Does.Not.Contain("Installationsdokumentation"), "an off section emits nothing");
            vm.ShowInstallation = true;
            vm.ShowEndUser = false;
            Assert.That(vm.Html, Does.Not.Contain("Funktionsdokumentation"), "end-user section off");
            vm.ShowEndUser = true;
            vm.ShowFunctionBlocks = false;
            Assert.That(vm.Html, Does.Not.Contain("Functionsblok dokumentation"), "function-block section off");
            vm.ShowFunctionBlocks = true;

            // Internal ids.
            Assert.That(vm.Html, Does.Not.Contain("Interne id'er"), "internal ids off by default");
            vm.ShowInternalIds = true;
            Assert.That(vm.Html, Does.Contain("Interne id'er").And.Contain("_0xABC"), "internal ids on shows the element ids");
            vm.ShowInternalIds = false;

            // Wire colours (Kabler column).
            Assert.That(vm.Html, Does.Contain("Grøn"), "wire colours on by default");
            vm.ShowWireColours = false;
            Assert.That(vm.Html, Does.Not.Contain("Grøn"), "wire colour hidden when off");
            vm.ShowWireColours = true;

            // Link display (terminal-detail).
            Assert.That(vm.Html, Does.Contain("Stue-blok"), "link display on by default");
            vm.ShowLinkDisplay = false;
            Assert.That(vm.Html, Does.Not.Contain("Stue-blok"), "link display hidden when off");
            vm.ShowLinkDisplay = true;

            // Function documentation (terminal-detail note).
            Assert.That(vm.Html, Does.Contain("Tænder lyset"), "function docs on by default");
            vm.ShowFunctionDocs = false;
            Assert.That(vm.Html, Does.Not.Contain("Tænder lyset"), "function docs hidden when off");
            vm.ShowFunctionDocs = true;

            // Show empty fields: the blank Projekt Number shows "--" when on, blank when off.
            Assert.That(vm.Html, Does.Contain("Nummer</th><td>--</td>"), "empty fields show the placeholder by default");
            vm.ShowEmptyFields = false;
            Assert.That(vm.Html, Does.Contain("Nummer</th><td></td>"), "empty field collapses to blank when off");
        });
    }

    // T030 / US-040: the purpose presets seed the switches — each shows the right sections and detail and hides the rest.
    [Test]
    public void ReportPreset_SeedsTheSwitchesForEachPurpose()
    {
        var vm = new ihc_openvisual.ViewModels.ReportsViewModel(SwitchTestReport(),
            (_, _) => Task.FromResult<string?>(null), _ => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            // Installation / technical: installation section only, cabling + link technical detail on, internal ids off.
            vm.ApplyPresetCommand.Execute(ReportPreset.Installation);
            Assert.That(vm.ShowInstallation, Is.True, "installation preset shows the installation section");
            Assert.That(vm.ShowEndUser || vm.ShowFunctionBlocks, Is.False, "installation preset hides the other sections");
            Assert.That(vm.ShowWireColours && vm.ShowLinkDisplay, Is.True, "installation preset keeps the technical detail");
            Assert.That(vm.ShowInternalIds, Is.False, "installation preset hides internal ids");
            Assert.That(vm.Html, Does.Contain("Installationsdokumentation").And.Not.Contain("Funktionsdokumentation"),
                "installation preset renders only the installation section");

            // End-user / function: end-user section only, function docs on, cabling/link detail dropped.
            vm.ApplyPresetCommand.Execute(ReportPreset.EndUser);
            Assert.That(vm.ShowEndUser, Is.True, "end-user preset shows the end-user section");
            Assert.That(vm.ShowInstallation || vm.ShowFunctionBlocks, Is.False, "end-user preset hides the other sections");
            Assert.That(vm.ShowFunctionDocs, Is.True, "end-user preset keeps function documentation");
            Assert.That(vm.ShowWireColours, Is.False, "end-user preset drops the wire colours");
            Assert.That(vm.Html, Does.Contain("Funktionsdokumentation").And.Not.Contain("Installationsdokumentation"),
                "end-user preset renders only the end-user section");

            // Function-block: function-block section only.
            vm.ApplyPresetCommand.Execute(ReportPreset.FunctionBlock);
            Assert.That(vm.ShowFunctionBlocks, Is.True, "function-block preset shows the function-block section");
            Assert.That(vm.ShowInstallation || vm.ShowEndUser, Is.False, "function-block preset hides the other sections");
            Assert.That(vm.Html, Does.Contain("Functionsblok dokumentation").And.Not.Contain("Installationsdokumentation"),
                "function-block preset renders only the function-block section");

            // Full: every section and every detail, including internal ids.
            vm.ApplyPresetCommand.Execute(ReportPreset.Full);
            Assert.That(vm.ShowInstallation && vm.ShowEndUser && vm.ShowFunctionBlocks, Is.True, "full preset shows every section");
            Assert.That(vm.ShowInternalIds, Is.True, "full preset shows internal ids");
            Assert.That(vm.Html, Does.Contain("Installationsdokumentation")
                    .And.Contain("Funktionsdokumentation").And.Contain("Functionsblok dokumentation").And.Contain("Interne id'er"),
                "full preset renders all three sections and internal ids");
        });
    }
}
