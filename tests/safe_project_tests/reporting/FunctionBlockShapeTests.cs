using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Reporting;
using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T010 (S01: builder tests assert SHAPES; the byte gates follow in T011/T012): the function-block
    /// report as a shape projection. Pins the element→icon-key resolution, the B7 heading/paragraph rules
    /// (heading = block @name; note line 1 dropped iff it equals @name trimmed; "Anvendelse:"/"Anvendes:"
    /// label lines dropped; the LAST paragraph renders note-styled when more than one remains), A10
    /// (= value only under settings/internalsettings) with the A11 per-type formats incl. B1's REAL month,
    /// the vendor-scope variable-type filter, A3/B8 independent %P/%S/%LT substitution, and the A12
    /// program-tree nesting with U6 (unknown conditions type = and) and U7 (stray program_sub/program_case
    /// directly under programs dropped).
    /// </summary>
    public class FunctionBlockShapeTests
    {
        private static readonly DateTimeOffset Clock = new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

        private static ProjectAppService App() => new(TestSetup.Settings);

        private static Project Load(string name) =>
            App().Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name)))).GetAwaiter().GetResult();

        private static ImmutableArray<FbBlockShape> Blocks(Project project) =>
            FunctionBlockReportBuilder.Build(project, Clock).Shapes.OfType<FbBlockShape>().ToImmutableArray();

        private static ImmutableArray<IconTreeRow> Rows(FbBlockShape block) =>
            block.Rows.Cast<IconTreeRow>().ToImmutableArray();

        [Test]
        public void Blocks_DocumentOrder_B7Headings_AndParagraphStyling()
        {
            ImmutableArray<FbBlockShape> blocks = Blocks(Load("project5-Dokumentation.vis"));

            Assert.Multiple(() =>
            {
                Assert.That(blocks.Select(b => b.Heading),
                    Is.EqualTo(new[] { "Doku zoo", "6.3.03.a. Overfaldstryk", "Tom blok" }),
                    "every block in flattened-locality document order, heading = the block's @name (B7)");
                Assert.That(blocks[0].Paragraphs.Select(p => (p.Text, p.IsNote)), Is.EqualTo(new[]
                {
                    ("Doku zoo: én af hver variabeltype og alle programformer.", false),
                    ("Dækker æøåÆØÅ i en funktionsbloknote.", false),
                }), "note line 1 ≠ @name is KEPT (B7); a sole paragraph renders plain");
                Assert.That(blocks[1].Paragraphs.Select(p => (p.Text, p.IsNote)), Is.EqualTo(new[]
                {
                    ("Anvendes til to tast kombinationstryk ifb. med aktivering af overfaldsalarm", false),
                    ("For at se den fulde beskrivelse marker denne linje og tryk \"F1\".", true),
                    ("Bemærk: Dette virker kun hvis \"Blokken\" er rød-markeret.", true),
                }), "the LAST paragraph is note-styled when more than one remains");
                Assert.That(blocks[2].Paragraphs, Is.Empty, "a block without a note has no paragraphs");
            });
        }

        [Test]
        public void Project3_B7BothBranches_AndLabelLineDrops()
        {
            ImmutableArray<FbBlockShape> blocks = Blocks(Load("project3-KompleksWired-enduserdoc.vis"));

            FbBlockShape kip = blocks.Single(b => b.Heading == "1.1.01.e. Kip tænd sluk");
            FbBlockShape ur = blocks.Single(b => b.Heading == "2.1.01.a. Ur, med 1 tidspunkt");
            FbBlockShape drift = blocks.Single(b => b.Heading == "4.1.04. Driftstimetæller");
            Assert.Multiple(() =>
            {
                Assert.That(kip.Paragraphs.Select(p => (p.Text, p.IsNote)), Is.EqualTo(new[]
                {
                    ("Kip, tænd og sluk af udgang, eller som trappeautomat", false),
                    ("For at se den fulde beskrivelse marker denne linje og tryk \"F1\".", true),
                    ("Bemærk: Dette virker kun hvis \"Blokken\" er rød-markeret.", true),
                }), "B7 drop branch: note line 1 equals @name → dropped; 'Anvendelse:' label dropped; trailing spaces trimmed");
                Assert.That(ur.Paragraphs[0].Text, Is.EqualTo("2.1.01.a. 7 dages ur, med 1 tænd tidspunkt"),
                    "B7 keep branch: note line 1 differs from @name → kept as the first body paragraph");
                Assert.That(drift.Paragraphs.Select(p => p.Text).Take(2), Is.EqualTo(new[]
                {
                    "Til at tælle time forbrug på f.eks lysrør, eller steder hvor forebyggende vedligehold ønskes.",
                    "Dette vises i Timer.",
                }), "the 'Anvendes:' label variant is dropped too");
            });
        }

        [Test]
        public void VariableRows_A10_A11_B1_AndVendorTypeFilter()
        {
            FbBlockShape zoo = Blocks(Load("project5-Dokumentation.vis"))[0];
            ImmutableArray<IconTreeRow> rows = Rows(zoo);

            int settings = rows.IndexOf(rows.Single(r => r.Name == "Indstillinger"));
            int internals = rows.IndexOf(rows.Single(r => r.Name == "Interne variable"));
            int programs = rows.IndexOf(rows.Single(r => r.Name == "Programmer"));
            Assert.Multiple(() =>
            {
                Assert.That(rows[settings].IconKey, Is.EqualTo("section-settings"));
                Assert.That(rows[internals].IconKey, Is.EqualTo("section-internal-vars"));
                Assert.That(rows.Skip(settings + 1).Take(internals - settings - 1)
                        .Select(r => (r.IconKey, r.Name, r.Value)), Is.EqualTo(new[]
                {
                    ("var-timer", "Timer", "00:03:00,000"),
                    ("var-time", "Tidspunkt", "07:30:00"),
                    ("var-timer-duration", "Timertid", "00:00:01,500"),
                    ("var-counter", "Tæller", "5"),
                    ("var-integer", "Tal", "42"),
                    ("var-enum", "Tilstand", "Tilstand B"),
                    ("var-date", "Dato", "30. juli"),
                    ("var-weekday", "Ugedag", "fredag"),
                }), "settings variables carry `= value` per the A11 type formats; B1 renders the REAL month");
                Assert.That(rows.Skip(internals + 1).Take(programs - internals - 1)
                        .Select(r => (r.IconKey, r.Name, r.Value, r.Membership)), Is.EqualTo(new[]
                {
                    ("var-flag", "Flag", "On", ReportMembership.Common),
                    ("var-holiday", "Helligdag", "On", ReportMembership.FullOnly),
                    ("var-temperature", "Temperatur", "21.50 C", ReportMembership.Common),
                    ("var-humidity", "Fugt", "45.00%", ReportMembership.FullOnly),
                    ("var-illuminance", "Lys", "300 Lux", ReportMembership.FullOnly),
                    ("var-light-level", "Lysniveau", "75%", ReportMembership.Common),
                    ("var-decimal", "Kommatal", "3.14", ReportMembership.Common),
                    ("kW", "kW", "1.25 kW", ReportMembership.FullOnly),
                    ("kWh", "kWh", "250.00 kWh", ReportMembership.FullOnly),
                    ("W", "W", "900.00 W", ReportMembership.FullOnly),
                    ("Wh", "Wh", "1500.00 Wh", ReportMembership.FullOnly),
                }.Select(r => (r.Item1 == "kW" || r.Item1 == "kWh" || r.Item1 == "W" || r.Item1 == "Wh"
                        ? "var-energy" : r.Item1, r.Item2, r.Item3, r.Item4))),
                    "RL-4/G2: every declared variable renders — the register-C1 types (holiday, humidity, "
                    + "illuminance, the four energy tags) tagged FullOnly so Standard keeps vendor parity. "
                    + "Units come from the catalog's own resource notes (\"0-100% RH\", \"0-60.000 Lux\") and, "
                    + "for the energy tags, from the tag itself — which is why all four share var-energy");
                Assert.That(rows.Take(settings).Select(r => (r.IconKey, r.Name, r.Value, r.Note)), Is.EqualTo(new (string, string, string?, string?)[]
                {
                    ("section-input", "Input", null, null),
                    ("pin-in", "Kip", null, "Tænd/sluk af stuelys"),
                    ("pin-in", "Spærre", null, "Blokerer tænd"),
                    ("section-output", "Output", null, null),
                    ("pin-out", "Udgang", null, "Til lampe og LED"),
                    ("scenario", "Scenarie", null, "Aften"),
                }), "pins/scenes carry their note and never a value (A10)");
            });
        }

        [Test]
        public void StatementRows_SubstitutePlaceholders_A3()
        {
            FbBlockShape zoo = Blocks(Load("project5-Dokumentation.vis"))[0];
            ImmutableArray<IconTreeRow> rows = Rows(zoo);

            int start = rows.IndexOf(rows.Single(r => r.Name == "Hændelser" && r.IconKey == "prog-program"));
            Assert.That(rows.Skip(start + 1).Take(13).Select(r => (r.IconKey, r.Name, r.Value)), Is.EqualTo(new[]
            {
                ("event-group", "Hændelser", (string?)null),
                ("event", "Kip -> ON", null),
                ("event", "Powerup", null),
                ("command-group", "Kommandoer", null),
                ("command", "Udgang", "ON"),
                ("command", "Tal", "7"),
                ("command", "Timer", "Timertid"),
                ("command", "Tilstand", "Tilstand C"),
                ("command", "Fremkald Scenarie", null),
                ("command", "Aktiver nedtælling på Timer med initial værdi", null),
                ("command", "Tidspunkt", "22:15:00"),
                ("command", "Dato", "24. december"),
                ("command", "Temperatur", "18.50 C"),
            }), "%P → link1 name; %S → inline constant VALUE (A11-formatted, B1 real month) or sibling NAME; " +
                "templates with ' = ' split into name/value");
        }

        [Test]
        public void ProgramTree_Nesting_A12_AndCaseActionScope()
        {
            FbBlockShape zoo = Blocks(Load("project5-Dokumentation.vis"))[0];
            ImmutableArray<IconTreeRow> rows = Rows(zoo);

            int start = rows.IndexOf(rows.Single(r => r.Name == "Betingelser" && r.IconKey == "prog-program"));
            Assert.That(rows.Skip(start).Take(12).Select(r => (r.Depth, r.IconKey, r.Name)), Is.EqualTo(new[]
            {
                (1, "prog-program", "Betingelser"),
                (2, "event-group", "Hændelser"),
                (3, "event", "Flag -> ON"),
                (2, "command-group", "Kommandoer"),
                (3, "prog-subprogram", "Under program"),
                (4, "cond-and", "Betingelser"),
                (5, "condition", "Udgang"),
                (5, "condition", "Tal"),
                (5, "cond-or", "Betingelser"),
                (6, "condition", "Tilstand"),
                (6, "condition", "Tilstand"),
                (4, "command-group", "Kommandoer ved betingelser sande"),
            }), "A12 nesting; the and/or group icon comes from the conditions group's type");

            int caseStart = rows.IndexOf(rows.Single(r => r.Name == "Case (Tilstand)"));
            Assert.That(rows.Skip(caseStart).Take(13).Select(r => (r.IconKey, r.Name, r.Value)), Is.EqualTo(new[]
            {
                ("prog-subprogram", "Case (Tilstand)", (string?)null),
                ("command-group", "Case Tilstand", "Tilstand A"),
                ("command", "Udgang", "ON"),
                ("command-group", "Case Tilstand", "Tilstand B"),
                ("command", "Udgang", "OFF"),
                // G3: the Tilstand B branch's sub-program, which used to be dropped with its whole subtree.
                ("prog-subprogram", "Under program", null),
                ("cond-and", "Betingelser", null),
                ("condition", "Tæller >= 5", null),
                ("command-group", "Kommandoer ved betingelser sande", null),
                ("command", "Flag", "ON"),
                ("command-group", "Kommandoer ved betingelser falske", null),
                ("command-group", "Udføres når ingen case er lig case værdien", null),
                ("command", "Kip Udgang", null),
            }), "%LT → the case selector's name; a case branch renders the same child vocabulary as an " +
                "ordinary actions group, so an authored sub-program inside one is documented (G3)");
        }

        /// <summary>
        /// RL-5 / finding G3: a <c>case_action</c> used to render only its <c>action</c> children, so a
        /// sub-program authored inside a case branch vanished from the report together with its whole
        /// subtree — the largest single content loss the generality review found, and witnessed by the
        /// vendor-authored project5 fixture. It now renders on the same terms as a sub-program under an
        /// ordinary <c>actions</c> group, in FULL mode only (register C2: this is content the vendor's own
        /// report loses, and Standard is the parity surface).
        /// </summary>
        [Test]
        public void CaseAction_RendersItsNestedSubProgram_FullOnly_G3()
        {
            ImmutableArray<IconTreeRow> rows = Rows(Blocks(Load("project5-Dokumentation.vis"))[0]);

            IconTreeRow[] subPrograms = [.. rows.Where(r => r.Name == "Under program")];

            Assert.Multiple(() =>
            {
                Assert.That(subPrograms.Select(r => r.Membership), Is.EqualTo(new[]
                {
                    ReportMembership.Common,
                    ReportMembership.Common,
                    ReportMembership.FullOnly,
                }), "G3: the block holds three sub-programs — two under ordinary actions groups, which were "
                    + "never lost and stay vendor-parity content, and one inside a case branch, which was "
                    + "dropped entirely and now renders in Full mode only");

                IconTreeRow nested = subPrograms[^1];
                Assert.That(rows.Skip(rows.IndexOf(nested))
                        .TakeWhile((row, index) => index == 0 || row.Depth > nested.Depth)
                        .Select(row => (Depth: row.Depth - nested.Depth, row.IconKey)), Is.EqualTo(new[]
                {
                    (0, "prog-subprogram"),
                    (1, "cond-and"),
                    (2, "condition"),
                    (1, "command-group"),
                    (2, "command"),
                    (1, "command-group"),
                }), "G3: and it brings its whole subtree with it — the conditions group, the true branch "
                    + "and the empty false branch");
            });
        }

        [Test]
        public void SyntheticRules_B7Drop_B8Independence_U6_U7()
        {
            ProjectElement root = Node("utcs_project", null, new[] { ("version_major", "4"), ("version_minor", "0"), ("last_unique_id", "_0xffff") },
                Node("groups", "_0x2031", new[] { ("name", "L") },
                    Node("group", "_0x2132", new[] { ("name", "Rum") },
                        Node("functionblock", "_0x100028", new[] { ("name", "Blok X"), ("note", "Blok X\nBrødtekst") },
                            Node("inputs", "_0x101023", new[] { ("name", "Input") },
                                Node("resource_input", "_0x102011", new[] { ("name", "Vælger") })),
                            Node("outputs", "_0x103024", new[] { ("name", "Output") }),
                            Node("settings", "_0x104025", new[] { ("name", "Indstillinger") }),
                            Node("internalsettings", "_0x105029", new[] { ("name", "Interne variable") }),
                            Node("programs", "_0x106026", new[] { ("name", "Programmer") },
                                Node("program_simple", "_0x10701e", new[] { ("name", "P1") },
                                    Node("events", "_0x108064", new[] { ("name", "Hændelser") }),
                                    Node("actions", "_0x109066", new[] { ("name", "Kommandoer") },
                                        Node("program_case", "_0x10a021", new[] { ("name", "Case (%LT) via %P"), ("link", "_0x102011"), ("link1", "_0x102011") }),
                                        Node("program_sub", "_0x10b01f", new[] { ("name", "Sub") },
                                            Node("conditions", "_0x10c065", new[] { ("name", "Betingelser"), ("type", "mystisk") }),
                                            Node("actions", "_0x10d066", new[] { ("name", "Sande") })))),
                                Node("program_sub", "_0x10e01f", new[] { ("name", "Løs sub") }),
                                Node("program_case", "_0x10f021", new[] { ("name", "Løs case") }))))));

            FbBlockShape block = Blocks(new Project(root)).Single();
            ImmutableArray<IconTreeRow> rows = Rows(block);
            Assert.Multiple(() =>
            {
                Assert.That(block.Paragraphs.Select(p => p.Text), Is.EqualTo(new[] { "Brødtekst" }),
                    "B7 drop branch: note line 1 equal to the block name (trimmed) is dropped");
                Assert.That(rows.Single(r => r.IconKey == "cond-and").Name, Is.EqualTo("Betingelser"),
                    "U6: a conditions type other than and/or renders as the AND group");
                Assert.That(rows.Select(r => r.Name), Has.None.EqualTo("Løs sub").And.None.EqualTo("Løs case"),
                    "U7: program_sub/program_case directly under programs are dropped");
                Assert.That(rows.Single(r => r.IconKey == "prog-subprogram" && r.Name.StartsWith("Case", StringComparison.Ordinal)).Name,
                    Is.EqualTo("Case (Vælger) via Vælger"),
                    "B8: %LT and %P substitute independently in one template");
            });
        }
    }
}
