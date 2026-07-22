using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Oracle-based regression gate for the render-ready report model (US-040 SDK enabler): asserts that the
    /// combined <see cref="ProjectAppService.GenerateProjectDocumentationReport"/> composes installation / end-user
    /// content that reproduces the committed vendor renders (REPORT-P3 output-spec.md) over the
    /// <c>project3-KompleksWired.vis</c> oracle and its populated-masthead sibling
    /// <c>project3-KompleksWired-projektinfo.vis</c>. Controller-free; the vendor XSLTs/HTML are the layout
    /// oracle only, never a runtime dependency. Model values are compared against their LOGICAL (unescaped)
    /// form — the GUI does the HTML escaping in its 1-to-1 transform.
    /// </summary>
    public class ReportBuilderTests
    {
        private const string Stue = "Stue & Køkken \"åben\"";
        private const string Note = "(Udfyldes af installatøren)";

        private static ProjectAppService App() => new(TestSetup.Settings);

        private static Project Load(string name) =>
            App().Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name)))).GetAwaiter().GetResult();

        // Re-sourced through the combined report (T032 retired the per-section entry points); the combined model
        // composes the same InstallationReport/EndUserReport, so these oracle assertions are unchanged.
        private static InstallationReport Installation(string name = "project3-KompleksWired.vis") =>
            App().GenerateProjectDocumentationReport(Load(name)).Installation;

        private static EndUserReport EndUser(string name = "project3-KompleksWired.vis") =>
            App().GenerateProjectDocumentationReport(Load(name)).EndUser;

        // ----- T020 / D14: the COMBINED project-documentation report — fixed section order + switch data -----

        [Test]
        public void Combined_HasFixedSectionOrder_ComposingTheThreeSubReports()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(Load("project3-KompleksWired.vis"));
            Assert.Multiple(() =>
            {
                Assert.That(report.Sections.Select(s => s.Kind),
                    Is.EqualTo(new[] { ReportSectionKind.Installation, ReportSectionKind.EndUser, ReportSectionKind.FunctionBlock }),
                    "the combined model orders its sections installation → end-user → function-block");
                Assert.That(report.Sections.All(s => s.Id.Length > 0 && s.IncludedByDefault), Is.True,
                    "each section carries a stable id and is included by default");
                Assert.That(report.Installation.Heading, Is.EqualTo("Installationsdokumentation"), "it composes the installation sub-report");
                Assert.That(report.FunctionBlock.Heading, Is.EqualTo("Functionsblok dokumentation"), "it composes the function-block sub-report");
            });
        }

        // T031 / D16: the combined report identifies each end-user product by its resolved catalog TYPE-NAME text
        // (image-free), not the raw product_identifier image key.
        [Test]
        public void Combined_ResolvesEndUserProductTypeName_ImageFree()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(Load("project3-KompleksWired.vis"));

            EndUserProduct firstProduct = report.EndUser.Localities[0].Products[0];
            Assert.Multiple(() =>
            {
                Assert.That(firstProduct.ProductType, Is.Not.Empty,
                    "the product type resolves to a catalog display-name text");
                Assert.That(firstProduct.ProductType, Does.Not.StartWith("_0x"),
                    "the resolved identity is a type NAME, never the raw image key");
            });
        }

        [Test]
        public void Combined_CarriesSwitchData_IdsRawBlanksInclusionFlagsLocalities()
        {
            Project project = Load("project3-KompleksWired.vis");
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(project);
            Assert.Multiple(() =>
            {
                // Unified localities: one per project group, each with its internal id + inclusion flag.
                Assert.That(report.Localities.Count(), Is.EqualTo(project.Groups.Count()), "one unified locality per project group");
                Assert.That(report.Localities.All(l => l.Id.Length > 0 && l.IncludedByDefault), Is.True,
                    "each locality carries its internal id and is included by default");

                // Per-element internal ids + inclusion flags: every product / function block is switchable.
                Assert.That(report.Elements, Is.Not.Empty, "products and function blocks are switchable elements");
                Assert.That(report.Elements.All(e => e.Id.Length > 0 && e.IncludedByDefault), Is.True,
                    "each element carries its internal id and is included by default");
                Assert.That(report.Elements.Any(e => e.Section == ReportSectionKind.FunctionBlock), Is.True, "function blocks are captured as elements");

                // Raw blank-state beside the display value: the masthead project name carries both.
                Assert.That(report.ProjectName.Display, Is.EqualTo(report.ProjectName.IsBlank ? "--" : report.ProjectName.Raw),
                    "the project name carries raw + display so the app can switch blank rendering without recomputing");
            });
        }

        // T023 / US-039: the combined model's Projekt identity block reads description / number / programmer off
        // project_info, each as a raw/display value (blank→"--").
        [Test]
        public void ProjektSection_PopulatesDescriptionNumberProgrammer_FromProjectInfo()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(Load("project3-KompleksWired-projektinfo.vis"));
            Assert.Multiple(() =>
            {
                Assert.That(report.Projekt.Number.Display, Is.EqualTo("num-A1"));
                Assert.That(report.Projekt.Description.Display, Does.StartWith("desc"), "the description reads off project_info@description");
                Assert.That(report.Projekt.Programmer.Display, Does.StartWith("prog"), "the programmer reads off project_info@programmer");
            });
        }

        [Test]
        public void ProjektSection_BlankFields_RenderAsPlaceholder()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(
                App().CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty)));
            Assert.That(report.Projekt.Number.Display, Is.EqualTo("--"), "a blank Projekt field shows the '--' placeholder");
        }

        // T024 / US-040 / US-073: the technical terminal detail resolves, for each linked dataline terminal, the
        // link-display path "-> <FB input> -> <function block> -> <its locality>" and the driving FB input's note.
        [Test]
        public void TerminalDetail_ResolvesLinkPathAndFunctionNote_OverProject3()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(Load("project3-KompleksWired.vis"));

            Assert.That(report.TerminalDetails, Is.Not.Empty, "project3 has linked dataline terminals");
            ReportTerminalDetail first = report.TerminalDetails[0];
            Assert.Multiple(() =>
            {
                Assert.That(first.LinkDisplay, Does.StartWith("-> "), "the link display starts with the arrow");
                Assert.That(CountArrows(first.LinkDisplay), Is.EqualTo(3), "the path is three hops: FB input -> function block -> its locality");
                Assert.That(report.TerminalDetails.Any(d => d.FunctionNote.Display.Length > 0 && d.FunctionNote.Display != "--"),
                    Is.True, "at least one terminal resolves a non-blank function note from its driving FB input");
            });
        }

        private static int CountArrows(string text) => (text.Length - text.Replace("->", string.Empty).Length) / 2;

        // T025 / US-073: the consolidated Kabler table is one row per ADDRESSED terminal (inputs + outputs),
        // in packed-address order, with unaddressed terminals excluded.
        [Test]
        public void Kabler_OneRowPerAddressedTerminal_InAddressOrder()
        {
            static bool Out(ProjectElement e) => e.Tag == "dataline_output";
            static bool Addressed(ProjectElement e) =>
                Ihc.Vis.Addressing.DatalineAddress.TryParse(e.GetAttribute("address_dataline"), Out(e), out _);
            static string Key(ProjectElement e)
            {
                string? token = e.GetAttribute("address_dataline");
                string hex = token is not null && token.StartsWith("_0x") ? token.Substring(3) : string.Empty;
                return hex.Length < 4 ? hex.PadLeft(4, '0') : hex;
            }

            // project3 has no assigned dataline addresses; Project1-SimpelWired carries the addressed terminals.
            Project project = Load("Project1-SimpelWired.vis");
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(project);

            var expected = project.Root.Descendants()
                .Where(e => e.Tag is "dataline_input" or "dataline_output" && Addressed(e))
                .OrderBy(Key, System.StringComparer.Ordinal)
                .Select(e => Ihc.Vis.Addressing.DatalineAddress.ToVendorLabel(e.GetAttribute("address_dataline"), Out(e)))
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(report.Kabler, Is.Not.Empty, "Project1-SimpelWired has addressed dataline terminals");
                Assert.That(report.Kabler.Select(r => r.Adresse), Is.EqualTo(expected),
                    "one Kabler row per addressed terminal, in packed-address order");
                Assert.That(report.Kabler.All(r => r.Adresse != "?"), Is.True, "unaddressed terminals are excluded");
            });
        }

        // T026 / US-073: the module section is a per-terminal address map — one occupancy row per addressed terminal,
        // naming the product terminal at each decoded address (reusing the SDK module-address projection).
        [Test]
        public void ModuleMap_OccupancyRows_OverAddressedProject()
        {
            // project3 has no assigned dataline addresses; Project1-SimpelWired carries the addressed terminals.
            Project project = Load("Project1-SimpelWired.vis");
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(project);

            int occupancy = report.ModuleMap.InputModules.Length + report.ModuleMap.OutputModules.Length;
            Assert.Multiple(() =>
            {
                Assert.That(occupancy, Is.EqualTo(5), "one occupancy row per addressed terminal (Project1 has 5)");
                Assert.That(report.ModuleMap.InputModules.Concat(report.ModuleMap.OutputModules).All(e => e.Address != "?" && e.Terminal.Length > 0),
                    Is.True, "each row decodes an address and names its occupying terminal");
            });
        }

        // T027 / US-072: a clean (empty) project reports no documentation issues.
        [Test]
        public void Completeness_CleanProject_NoIssues()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(
                App().CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty)));

            Assert.That(report.Completeness, Is.Empty, "an empty project has no wired products, so no documentation issues (renders 'none found')");
        }

        // T027 / US-072: over a project with known gaps, each issue is reported, located by locality/product, and
        // fully-documented aspects (a linked terminal) are omitted.
        [Test]
        public void Completeness_ReportsKnownGaps_AndOmitsDocumentedElements()
        {
            Project project = Load("project3-KompleksWired.vis");
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(project);

            int totalTerminals = project.Root.Descendants().Count(e => e.Tag is "dataline_input" or "dataline_output");
            int unlinked = report.Completeness.Count(r => r.Problem == "Ikke forbundet");
            Assert.Multiple(() =>
            {
                Assert.That(report.Completeness, Is.Not.Empty, "project3 has documentation gaps");
                Assert.That(report.Completeness.Select(r => r.Problem),
                    Does.Contain("Mangler Adresse").And.Contain("Mangler Id-kode").And.Contain("Mangler Ledningsfarve"),
                    "the known missing items are reported");
                Assert.That(unlinked, Is.LessThan(totalTerminals),
                    "a linked terminal is omitted from 'Ikke forbundet' — fully-documented aspects are not reported");
                Assert.That(report.Completeness.All(r => r.Locality.Length > 0 && r.Product.Length > 0), Is.True,
                    "each issue is located by locality → product");
            });
        }

        // T028 / US-041: the deep function-block layout carries the description, input/output notes, settings and
        // internal variables (name=value) and a flattened program outline.
        [Test]
        public void FunctionBlockReport_Deep_HasDescriptionNotesVariablesAndOutline()
        {
            ProjectDocumentationReport report = App().GenerateProjectDocumentationReport(Load("project3-KompleksWired.vis"));

            ReportFbBlock block = report.FunctionBlocks.First(b => !b.IsEmpty);
            Assert.Multiple(() =>
            {
                Assert.That(report.FunctionBlocks, Is.Not.Empty, "project3 has function blocks");
                Assert.That(block.Description, Does.Contain("Anvendelse"), "the description comes from the block note");
                Assert.That(block.Inputs.Any(p => p.Note.Length > 0), Is.True, "inputs carry their notes");
                Assert.That(block.Outputs, Is.Not.Empty, "outputs are captured");
                Assert.That(block.Settings.Concat(block.InternalVariables), Is.Not.Empty, "settings/internal variables are captured");
                Assert.That(block.Outline.Any(l => l.StartsWith("Program:")), Is.True, "the program outline names the programs");
                Assert.That(block.Outline.Any(l => l.Contains("Kommando:")), Is.True, "commands appear in the outline");
            });
        }

        [Test]
        public void FunctionBlockReport_Deep_UnprogrammedBlock_IsEmpty()
        {
            var svc = App();
            Project project = svc.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));
            var session = new ProjectDocumentSession();
            session.Open(project);
            ElementId localityId = project.Groups[0].Id!.Value;
            session.Apply(svc.Commands.AddEmptyFunctionBlock(session.Current!, localityId, "Tom"));

            ProjectDocumentationReport report = svc.GenerateProjectDocumentationReport(session.Current!);

            Assert.That(report.FunctionBlocks.Any(b => b.Name == "Tom" && b.IsEmpty), Is.True,
                "a function block with no programs is flagged empty (Tom blok)");
        }

        // ----- Masthead (§1.2): blank→"--", only Navn/Adresse/Telefon -----

        [Test]
        public void Installation_Masthead_RendersOnlyNameAddressPhone_BlankAsPlaceholder()
        {
            InstallationReport report = Installation();
            Assert.Multiple(() =>
            {
                Assert.That(report.Heading, Is.EqualTo("Installationsdokumentation"));
                Assert.That(report.Installer, Is.EqualTo(new ReportPartyInfo("Morten", "--", "--")));
                Assert.That(report.Customer, Is.EqualTo(new ReportPartyInfo("--", "--", "--")));
            });
        }

        [Test]
        public void Installation_Masthead_PopulatedProjektinfoVariant_FillsAllThreeFields()
        {
            InstallationReport report = Installation("project3-KompleksWired-projektinfo.vis");
            Assert.Multiple(() =>
            {
                Assert.That(report.Installer, Is.EqualTo(new ReportPartyInfo("instNavn-A1", "instVej-A1", "instTlf-A1")));
                Assert.That(report.Customer, Is.EqualTo(new ReportPartyInfo("kundeNavn-A1", "kundeVej-A1", "kundeTlf-A1")));
            });
        }

        // ----- Per-product detail tables (§3): every product, Installation-pane document order -----

        [Test]
        public void Installation_ProductDetails_ListAllTwelveProductsInDocumentOrder()
        {
            InstallationReport report = Installation();
            (ReportProductKind Kind, string Name)[] expected =
            {
                (ReportProductKind.Dataline, "LK FUGA Tryk 2 tast"),
                (ReportProductKind.Dataline, "Lampeudtag"),
                (ReportProductKind.Dataline, "Diode"),
                (ReportProductKind.Dataline, "PIR"),
                (ReportProductKind.Dataline, "Stikkontakt"),
                (ReportProductKind.Dataline, "Temperatur sensor med logning"),
                (ReportProductKind.Dataline, "Lux / Temperatur sensor med logning"),
                (ReportProductKind.Dataline, "Dimmer touch"),
                (ReportProductKind.Airlink, "Dimmer Universal"),
                (ReportProductKind.Airlink, "Tryk 2 tast"),
                (ReportProductKind.Airlink, "Lampeudtag"),
                (ReportProductKind.Rs485LedDimmer, "IHC LED Dimmer 2 kanaler"),
            };
            (ReportProductKind, string)[] actual = report.ProductDetails
                .Select(p => (p.Kind, Component(p)))
                .ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(report.ProductDetails, Has.Length.EqualTo(12));
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(report.ModemDetails, Is.Empty, "no modems in project3");
            });
        }

        [Test]
        public void Installation_DatalineProductDetail_HasSevenLabelRowsAndTerminalSubtable()
        {
            ProductDetailTable lkFuga = Installation().ProductDetails[0];
            Assert.Multiple(() =>
            {
                Assert.That(lkFuga.Rows.Select(r => (r.Label, r.Value)), Is.EqualTo(new[]
                {
                    ("Lokalitet", Stue),
                    ("Placering", "--"),
                    ("Komponent", "LK FUGA Tryk 2 tast"),
                    ("Identifikationskode", "--"),
                    ("Kabelnummer", "--"),
                    ("Kabeltype", "--"),
                    ("Lysgruppe", "--"),
                }));
                Assert.That(lkFuga.Terminals, Is.EqualTo(new[]
                {
                    new ReportTerminalRow("Tryk (venstre)", "Indgang ?", "--"),
                    new ReportTerminalRow("Tryk (højre)", "Indgang ?", "--"),
                }));
            });
        }

        [Test]
        public void Installation_OutputTerminal_UsesUdgangDirectionWord()
        {
            ProductDetailTable diode = Installation().ProductDetails[2];   // Diode
            Assert.Multiple(() =>
            {
                Assert.That(Component(diode), Is.EqualTo("Diode"));
                Assert.That(diode.Terminals, Is.EqualTo(new[] { new ReportTerminalRow("Lampe", "Udgang ?", "--") }));
            });
        }

        [Test]
        public void Installation_AirlinkDetail_UsesReducedLabelSetWithSerialNumber_NoTerminals()
        {
            ProductDetailTable dimmerUniversal = Installation().ProductDetails[8];
            Assert.Multiple(() =>
            {
                Assert.That(dimmerUniversal.Kind, Is.EqualTo(ReportProductKind.Airlink));
                Assert.That(dimmerUniversal.Rows.Select(r => (r.Label, r.Value)), Is.EqualTo(new[]
                {
                    ("Lokalitet", "Soveværelse"),
                    ("Placering", "--"),
                    ("Komponent", "Dimmer Universal"),
                    ("Identifikationskode", "--"),
                    ("Serie nummer", "_0x0"),   // present literal value, not blank
                    ("Lysgruppe", "--"),
                }));
                Assert.That(dimmerUniversal.Terminals, Is.Empty);
            });
        }

        [Test]
        public void Installation_LedDimmerDetail_HasFourLabelRowsOnly()
        {
            ProductDetailTable ledDimmer = Installation().ProductDetails[11];
            Assert.Multiple(() =>
            {
                Assert.That(ledDimmer.Kind, Is.EqualTo(ReportProductKind.Rs485LedDimmer));
                Assert.That(ledDimmer.Rows.Select(r => (r.Label, r.Value)), Is.EqualTo(new[]
                {
                    ("Lokalitet", "Kælder"),
                    ("Placering", "--"),
                    ("Komponent", "IHC LED Dimmer 2 kanaler"),
                    ("Serie nummer", "--"),
                }));
            });
        }

        // ----- Flat cross-reference tables (§5/§6): blank→empty cell, product columns joined -----

        [Test]
        public void Installation_DatalineInputs_FlatTable_FiveRowsWithEmptyBlanks()
        {
            ImmutableArray<DatalineCrossReferenceRow> rows = Installation().DatalineInputs;
            (string Product, string Terminal, string Locality)[] expected =
            {
                ("LK FUGA Tryk 2 tast", "Tryk (venstre)", Stue),
                ("LK FUGA Tryk 2 tast", "Tryk (højre)", Stue),
                ("PIR", "Tilstedeværelses indikering", "Entré"),
                ("Temperatur sensor med logning", "Temperatur sensor indgang", "Køkken"),
                ("Lux / Temperatur sensor med logning", "Lux / Temperatur sensor", "Køkken"),
            };
            Assert.Multiple(() =>
            {
                Assert.That(rows.Select(r => (r.Product, r.Terminal, r.Locality)), Is.EqualTo(expected));
                // Unassigned address → "?"; every unfilled product/terminal column is an empty cell, not "--".
                DatalineCrossReferenceRow first = rows[0];
                Assert.That(first.Address, Is.EqualTo("?"));
                Assert.That(new[] { first.Note, first.Position, first.IdCode, first.CableType, first.CableNumber, first.PowerGroup, first.WireColour },
                    Is.All.EqualTo(string.Empty));
            });
        }

        [Test]
        public void Installation_DatalineOutputs_FlatTable_FiveRowsInDocumentOrder()
        {
            ImmutableArray<DatalineCrossReferenceRow> rows = Installation().DatalineOutputs;
            (string Product, string Terminal, string Locality)[] expected =
            {
                ("Lampeudtag", "Udgang", Stue),
                ("Diode", "Lampe", Stue),
                ("Stikkontakt", "Udgang", "Entré"),
                ("Dimmer touch", "Touch", "Soveværelse"),
                ("Dimmer touch", "Sluk", "Soveværelse"),
            };
            Assert.That(rows.Select(r => (r.Product, r.Terminal, r.Locality)), Is.EqualTo(expected));
        }

        // ----- Special-product, S0 and module tables (§7/§8/§1/§2) -----

        [Test]
        public void Installation_EmptySections_RenderNoRows()
        {
            InstallationReport report = Installation();
            Assert.Multiple(() =>
            {
                Assert.That(report.InputModules, Is.Empty);
                Assert.That(report.OutputModules, Is.Empty);
                Assert.That(report.SpecialProducts, Is.Empty);
            });
        }

        [Test]
        public void Installation_S0Device_SingleRow_WithLocalityFromParentGroup()
        {
            ImmutableArray<S0DeviceRow> rows = Installation().S0Devices;
            Assert.That(rows, Has.Length.EqualTo(1));
            Assert.That(rows[0], Is.EqualTo(new S0DeviceRow(
                Product: "S0 Device", Note: "PRODUCT_2315_NOTE", Locality: "Kælder",
                Position: "", IdCode: "", CableColourS0Minus: "", CableColourS0Plus: "")));
        }

        // ----- End-user report (§2): localities, omission filter, note propagation -----

        [Test]
        public void EndUser_ListsAllElevenLocalities_InDocumentOrder_NeverOmitted()
        {
            EndUserReport report = EndUser();
            Assert.Multiple(() =>
            {
                Assert.That(report.Heading, Is.EqualTo("Funktionsdokumentation"));
                Assert.That(report.Localities.Select(l => l.Name), Is.EqualTo(new[]
                {
                    Stue, "Entré", "Køkken", "Soveværelse", "Værelse",
                    "Bad", "Bryggers", "Garage", "Kælder", "Udendørs", "Lokalitet",
                }));
                // The screen anchor is the group/@id token; the print transform anchors on Name instead.
                Assert.That(report.Localities[0].AnchorId, Is.EqualTo("_0x2132"));
            });
        }

        [Test]
        public void EndUser_OmissionFilter_ShowsOnlyEnduserFlaggedProducts()
        {
            EndUserReport report = EndUser();
            EndUserLocality stue = report.Localities[0];
            EndUserLocality vaerelse = report.Localities[4];
            Assert.Multiple(() =>
            {
                Assert.That(stue.Products.Select(p => p.Name), Is.EqualTo(new[] { "LK FUGA Tryk 2 tast", "Diode" }));
                Assert.That(vaerelse.Products.Select(p => p.Name), Is.EqualTo(new[] { "Tryk 2 tast" }));
                // Every other locality drops its products (9 of 12 products are unflagged).
                Assert.That(report.Localities.Where((_, i) => i != 0 && i != 4).SelectMany(l => l.Products), Is.Empty);
            });
        }

        [Test]
        public void EndUser_NotePropagation_RepeatsFbInputNoteOncePerLink_WithScreenOnlyFbLocality()
        {
            EndUserProduct lkFuga = EndUser().Localities[0].Products[0];
            Assert.Multiple(() =>
            {
                Assert.That(lkFuga.Position, Is.EqualTo(""), "blank position is dropped");
                Assert.That(lkFuga.Terminals.Select(t => t.Name), Is.EqualTo(new[] { "Tryk (venstre)", "Tryk (højre)" }));

                // "Tryk (venstre)" drives the FB input through two links → the note prints twice.
                EndUserTerminal venstre = lkFuga.Terminals[0];
                Assert.That(venstre.Notes, Is.EqualTo(new[]
                {
                    new EndUserNote(Note, "Værelse"),
                    new EndUserNote(Note, "Værelse"),
                }));
                // "Tryk (højre)" → one link → the note once.
                Assert.That(lkFuga.Terminals[1].Notes, Is.EqualTo(new[] { new EndUserNote(Note, "Værelse") }));
            });
        }

        [Test]
        public void EndUser_UnlinkedTerminal_HasNoNoteSubLines()
        {
            EndUserReport report = EndUser();
            // Diode's "Lampe" output is unlinked; the airlink "Tryk 2 tast" inputs are unlinked.
            EndUserProduct diode = report.Localities[0].Products[1];
            EndUserProduct tryk2 = report.Localities[4].Products[0];
            Assert.Multiple(() =>
            {
                Assert.That(diode.Terminals.Single().Name, Is.EqualTo("Lampe"));
                Assert.That(diode.Terminals.Single().Notes, Is.Empty);
                Assert.That(tryk2.Terminals.Select(t => t.Name), Is.EqualTo(new[] { "Tryk (venstre)", "Tryk (højre)" }));
                Assert.That(tryk2.Terminals.SelectMany(t => t.Notes), Is.Empty);
            });
        }

        // ----- Address decoding (§1.7): boundary values via a synthetic project -----

        [TestCase("_0x0", 16, "?")]
        [TestCase("_0x1", 16, "1.01")]
        [TestCase("_0x08", 16, "1.08")]   // bit 7 → "0" + (bit+1)
        [TestCase("_0x09", 16, "1.11")]   // bit 8 → bit+3
        [TestCase("_0x10", 16, "1.18")]   // bit 15 → bit+3
        [TestCase("_0x11", 16, "2.01")]   // next data line
        public void AddressDecoding_InputDivider16_MatchesVendorFormula(string address, int _, string expected)
        {
            ReportTerminalRow row = SyntheticTerminalRow("dataline_input", address);
            Assert.That(row.Address, Is.EqualTo("Indgang " + expected));
        }

        [TestCase("_0x1", 8, "1.01")]
        [TestCase("_0x08", 8, "1.08")]
        [TestCase("_0x09", 8, "2.01")]    // divider 8 → data line increments sooner
        public void AddressDecoding_OutputDivider8_MatchesVendorFormula(string address, int _, string expected)
        {
            ReportTerminalRow row = SyntheticTerminalRow("dataline_output", address);
            Assert.That(row.Address, Is.EqualTo("Udgang " + expected));
        }

        // ----- helpers -----

        private static string Component(ProductDetailTable table) =>
            table.Rows.Single(r => r.Label == "Komponent").Value;

        private static ReportTerminalRow SyntheticTerminalRow(string terminalTag, string address)
        {
            ProjectElement terminal = Element(terminalTag, ("name", "T"), ("address_dataline", address));
            ProjectElement product = new("product_dataline", null,
                ImmutableArray<(string, string)>.Empty, ImmutableArray.Create(terminal));
            ProjectElement root = new("utcs_project", null,
                ImmutableArray<(string, string)>.Empty, ImmutableArray.Create(product));
            InstallationReport report = App().GenerateProjectDocumentationReport(new Project(root)).Installation;
            return report.ProductDetails.Single().Terminals.Single();
        }

        private static ProjectElement Element(string tag, params (string, string)[] attrs) =>
            new(tag, null, attrs.ToImmutableArray(), ImmutableArray<ProjectElement>.Empty);
    }
}
