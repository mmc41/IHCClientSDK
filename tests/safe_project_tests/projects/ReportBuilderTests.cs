using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Oracle-based regression gate for the render-ready report model (US-040 SDK enabler): asserts that
    /// <see cref="ProjectAppService.GenerateInstallationReport"/> / <see cref="ProjectAppService.GenerateEndUserReport"/>
    /// reproduce the content of the committed vendor renders (REPORT-P3 output-spec.md) over the
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

        private static InstallationReport Installation(string name = "project3-KompleksWired.vis") =>
            App().GenerateInstallationReport(Load(name));

        private static EndUserReport EndUser(string name = "project3-KompleksWired.vis") =>
            App().GenerateEndUserReport(Load(name));

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
                Assert.That(lkFuga.ProductIdentifier, Is.EqualTo("_0x2101"));
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
            InstallationReport report = App().GenerateInstallationReport(new Project(root));
            return report.ProductDetails.Single().Terminals.Single();
        }

        private static ProjectElement Element(string tag, params (string, string)[] attrs) =>
            new(tag, null, attrs.ToImmutableArray(), ImmutableArray<ProjectElement>.Empty);
    }
}
