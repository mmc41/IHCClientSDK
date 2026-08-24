using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T060 — the five remaining PROJECT-STRUCTURE rows, and the sixth's deliberate absence.
    ///
    /// <para><b>The measurement that shaped this set</b> is <c>struct-product-no-terminals</c>: reading it as "no
    /// <c>dataline_*</c> child" reports every RS485 dimmer and every logging bus sensor in the corpus, because their
    /// wirable members are channels and measured values. Reading it as "nothing wirable at all" reports the SMS
    /// modem and nothing else. <see cref="ADimmerAndABusSensorAreNotTerminalLess"/> is that claim.</para>
    ///
    /// <para><b><c>struct-modified-stale</c> has no rule</b>, and <see cref="TheModifiedStaleRowHasNoRule"/> keeps
    /// that deliberate: <c>modified</c> is re-stamped on every save and no edit route touches it, so the condition
    /// cannot hold in a saved file.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProjectStructureRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── struct-locality-empty and struct-locality-no-devices ────────────────────────────────────

        [Test]
        public void AnEmptyLocalityIsReportedAndAFittedOneIsNot()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Locality(products: 0, blocks: 0), "struct-locality-empty"), Is.EqualTo(1));
                Assert.That(Message(Locality(products: 0, blocks: 0), "struct-locality-empty"),
                    Is.EqualTo("Lokaliteten 'Stue' indeholder hverken produkter eller blokke."));
                Assert.That(Count(Locality(products: 1, blocks: 0), "struct-locality-empty"), Is.Zero);
                Assert.That(Count(Locality(products: 0, blocks: 1), "struct-locality-empty"), Is.Zero,
                    "a room with logic is not empty, even without hardware");
            });
        }

        [Test]
        public void ALocalityWithOnlyBlocksIsTheOtherRow()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Locality(products: 0, blocks: 1), "struct-locality-no-devices"), Is.EqualTo(1));
                Assert.That(Message(Locality(products: 0, blocks: 1), "struct-locality-no-devices"),
                    Is.EqualTo("Lokaliteten 'Stue' indeholder kun funktionsblokke."));
                Assert.That(Count(Locality(products: 1, blocks: 1), "struct-locality-no-devices"), Is.Zero);
                Assert.That(Count(Locality(products: 0, blocks: 0), "struct-locality-no-devices"), Is.Zero,
                    "an empty room is the other row's finding; the two never both fire");
            });
        }

        [Test]
        public void TheEmptyProjectSkeletonIsTenEmptyRooms()
        {
            Assert.That(Count(Authentic("Project0-Tomt.vis"), "struct-locality-empty"), Is.EqualTo(10),
                "a new project ships ten named localities; the row is true about every one of them, which is what "
                + "its 'room planned but not yet fitted' disagreement is for");
        }

        // ── struct-product-no-terminals ─────────────────────────────────────────────────────────────

        [Test]
        public void AProductWithNothingWirableIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Product("sms_modem_settings"), "struct-product-no-terminals"), Is.EqualTo(1));
                Assert.That(Message(Product("sms_modem_settings"), "struct-product-no-terminals"),
                    Is.EqualTo("Produktet 'Produkt' har ingen klemmer."));
                Assert.That(Count(Product("dataline_input"), "struct-product-no-terminals"), Is.Zero);
            });
        }

        /// <summary>
        /// The measurement that shaped the row: a dimmer's channels and a bus sensor's measured values are what an
        /// author wires, so neither product is terminal-less.
        /// </summary>
        [Test]
        public void ADimmerAndABusSensorAreNotTerminalLess()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Product("rs485_led_dimmer_channel"), "struct-product-no-terminals"), Is.Zero,
                    "a channel is wirable");
                Assert.That(Count(Product("resource_temperature"), "struct-product-no-terminals"), Is.Zero,
                    "so is a bus sensor's measured value");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "struct-product-no-terminals"), Is.Zero,
                    "project3 holds a dimmer and two logging sensors, and reports none of them");
                Assert.That(Count(Authentic("project5-Dokumentation.vis"), "struct-product-no-terminals"),
                    Is.EqualTo(1), "only the SMS modem");
            });
        }

        // ── struct-orphan-block ─────────────────────────────────────────────────────────────────────

        [Test]
        public void AnUnreachedBlockIsReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Block(reach: Reach.Nothing), "struct-orphan-block"), Is.EqualTo(1));
                Assert.That(Message(Block(reach: Reach.Nothing), "struct-orphan-block"),
                    Is.EqualTo("Blokken 'Blok' er ikke forbundet til resten af installationen."));
                Assert.That(Count(Block(reach: Reach.Wire), "struct-orphan-block"), Is.Zero, "a wire reaches it");
                // The referring block is itself unreached, so the project reports IT and not the block under
                // test: naming the finding is what proves which of the two the rule means.
                Assert.That(Validate(Block(reach: Reach.Reference)).Findings
                    .Where(f => f.RuleId == "struct-orphan-block").Select(f => f.Message),
                    Is.EqualTo(new[] { "Blokken 'Blok B' er ikke forbundet til resten af installationen." })
                        .AsCollection,
                    "a reference from outside reaches the block under test; the referrer itself is the orphan");
            });
        }

        [Test]
        public void ABlockReferencingItselfIsStillAnOrphan()
        {
            Assert.That(Count(Block(reach: Reach.SelfReference), "struct-orphan-block"), Is.EqualTo(1),
                "its own program naming its own variable is not the rest of the installation reaching in");
        }

        [Test]
        public void TheAuthenticCorpusReportsOnlyIsolatedBlocks()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Authentic("Project1-SimpelWired.vis"), "struct-orphan-block"), Is.Zero,
                    "both its blocks are wired");
                Assert.That(Count(Authentic("project3-KompleksWired.vis"), "struct-orphan-block"), Is.EqualTo(8),
                    "eight of its nine blocks really are unwired — the file carries three wired pin pairs in total");
            });
        }

        // ── struct-icon-default ─────────────────────────────────────────────────────────────────────

        [Test]
        public void AnElementWithoutAnIconIsReportedOnlyWhereItsKindHasOne()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Icons(secondHasIcon: false), "struct-icon-default"), Is.Zero,
                    "no element of that kind carries an icon, so there is nothing to deviate from");
                Assert.That(Count(Icons(secondHasIcon: true), "struct-icon-default"), Is.EqualTo(1),
                    "one locality has an icon and the other does not");
                Assert.That(Message(Icons(secondHasIcon: true), "struct-icon-default"),
                    Is.EqualTo("Elementet 'Uden ikon' har ikke fået et ikon."));
            });
        }

        [Test]
        public void TheNullIconTokenCountsAsNoIcon()
        {
            Assert.That(Count(Icons(secondHasIcon: true, explicitNullToken: true), "struct-icon-default"),
                Is.EqualTo(1),
                "the DTD's default IS the null token, so carrying it explicitly is the same state as omitting it");
        }

        [Test]
        public void NoCommittedProjectCarriesTheIconState()
        {
            Assert.Multiple(() =>
            {
                foreach (string file in new[]
                    { "Project1-SimpelWired.vis", "project3-KompleksWired.vis", "project5-Dokumentation.vis",
                      "Project6-Errors.vis" })
                {
                    Assert.That(Count(Authentic(file), "struct-icon-default"), Is.Zero, file);
                }
            });
        }

        // ── the row that is NOT implemented ─────────────────────────────────────────────────────────

        [Test]
        public void TheModifiedStaleRowHasNoRule()
        {
            ProblemCode code = new("struct-modified-stale");

            Assert.Multiple(() =>
            {
                Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True,
                    "the id stays reserved");
                Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.RuledOut),
                    "`modified` is re-stamped on every save and no edit route touches it, so the condition cannot "
                    + "hold in a saved file");
                Assert.That(ProjectRules.All(ProblemCatalog.Current).Any(r => r.Entry.Code == code), Is.False);
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(TestSetup.Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project InGroups(params ProjectElement[] localities) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")], localities));

        /// <summary>One locality holding the given number of products and blocks.</summary>
        private static Project Locality(int products, int blocks) =>
            InGroups(
                Tree.Node("group", Token("group", 0x21), [("name", "Stue"), ("icon", "_0x15")],
                    [
                        .. Enumerable.Range(0, products).Select(i => Tree.Node("product_dataline",
                            Token("product_dataline", 0x40 + i),
                            [("product_identifier", "_0x2202"), ("name", $"Produkt {i}"), ("icon", "_0x83")],
                            Tree.Node("dataline_input", Token("dataline_input", 0x50 + i),
                                [("name", "Klemme"), ("icon", "_0x83")]))),
                        .. Enumerable.Range(0, blocks).Select(i => BlockShell(0x70 + (i * 0x10), $"Blok {i}", [])),
                    ]));

        /// <summary>A product whose only child is of the given tag.</summary>
        private static Project Product(string childTag) =>
            InGroups(
                Tree.Node("group", Token("group", 0x21), [("name", "Stue"), ("icon", "_0x15")],
                    Tree.Node("product_dataline", Token("product_dataline", 0x40),
                        [("product_identifier", "_0x2202"), ("name", "Produkt"), ("icon", "_0x83")],
                        Tree.Node(childTag, Token(childTag, 0x50), [("name", "Del"), ("icon", "_0x83")]))));

        private static ProjectElement BlockShell(int at, string name, ProjectElement[] internals) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name), ("icon", "_0xf")],
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")]),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")]),
                Tree.Node("settings", Token("settings", at + 3), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "Interne")], internals),
                Tree.Node("programs", Token("programs", at + 5), [("name", "Programmer")]));

        /// <summary>How the outside world reaches the block under test.</summary>
        private enum Reach
        {
            Nothing,
            Wire,
            Reference,
            SelfReference,
        }

        /// <summary>One block, reached (or not) in the given way.</summary>
        private static Project Block(Reach reach)
        {
            ProjectElement pin = reach == Reach.Wire
                ? Tree.Node("resource_input", Token("resource_input", 0x80),
                    [("name", "Indgang"), ("note", "N"), ("icon", "_0x36")],
                    Tree.Node("link_to_resource", Token("link_to_resource", 0x88),
                        [("name", "Link"), ("link", Token("link_from_resource", 0x89))]))
                : Tree.Node("resource_input", Token("resource_input", 0x80),
                    [("name", "Indgang"), ("note", "N"), ("icon", "_0x36")]);

            ProjectElement block = Tree.Node("functionblock", Token("functionblock", 0x70),
                [("name", "Blok"), ("icon", "_0xf")],
                Tree.Node("inputs", Token("inputs", 0x71), [("name", "Input")], pin),
                Tree.Node("outputs", Token("outputs", 0x72), [("name", "Output")]),
                Tree.Node("settings", Token("settings", 0x73), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", 0x74), [("name", "Interne")]),
                Tree.Node("programs", Token("programs", 0x75), [("name", "Programmer")],
                    reach == Reach.SelfReference
                        ? [Tree.Node("program_simple", Token("program_simple", 0x90), [("name", "Program")],
                            Tree.Node("events", Token("events", 0x91), [("name", "Hændelser")],
                                Tree.Node("event", Token("event", 0x92),
                                    [("name", "%P -> ON"), ("link1", Token("resource_input", 0x80)),
                                     ("method", "_0xa")])),
                            Tree.Node("actions", Token("actions", 0x93),
                                [("name", "Kommandoer"), ("type", "_0x2")]))]
                        : []));

            // a second block whose program names the first block's pin: a reference from outside
            ProjectElement referrer = Tree.Node("functionblock", Token("functionblock", 0xa0),
                [("name", "Blok B"), ("icon", "_0xf")],
                Tree.Node("inputs", Token("inputs", 0xa1), [("name", "Input")]),
                Tree.Node("outputs", Token("outputs", 0xa2), [("name", "Output")]),
                Tree.Node("settings", Token("settings", 0xa3), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", 0xa4), [("name", "Interne")]),
                Tree.Node("programs", Token("programs", 0xa5), [("name", "Programmer")],
                    Tree.Node("program_simple", Token("program_simple", 0xb0), [("name", "Program")],
                        Tree.Node("events", Token("events", 0xb1), [("name", "Hændelser")],
                            Tree.Node("event", Token("event", 0xb2),
                                [("name", "%P -> ON"), ("link1", Token("resource_input", 0x80)),
                                 ("method", "_0xa")])),
                        Tree.Node("actions", Token("actions", 0xb3),
                            [("name", "Kommandoer"), ("type", "_0x2")]))));

            return InGroups(
                Tree.Node("group", Token("group", 0x21), [("name", "Stue"), ("icon", "_0x15")],
                    reach == Reach.Reference ? [block, referrer] : [block]));
        }

        /// <summary>Two localities; the second either carries an icon, or the first's is the only one.</summary>
        private static Project Icons(bool secondHasIcon, bool explicitNullToken = false)
        {
            (string, string)[] without = explicitNullToken
                ? [("name", "Uden ikon"), ("icon", ElementId.NullToken)]
                : [("name", "Uden ikon")];

            return InGroups(
                Tree.Node("group", Token("group", 0x21),
                    secondHasIcon ? [("name", "Med ikon"), ("icon", "_0x15")] : [("name", "Med ikon")],
                    Tree.Node("product_dataline", Token("product_dataline", 0x40),
                        [("product_identifier", "_0x2202"), ("name", "Produkt"), ("icon", "_0x83")],
                        Tree.Node("dataline_input", Token("dataline_input", 0x50),
                            [("name", "Klemme"), ("icon", "_0x83")]))),
                Tree.Node("group", Token("group", 0x22), without,
                    Tree.Node("product_dataline", Token("product_dataline", 0x41),
                        [("product_identifier", "_0x2202"), ("name", "Produkt 2"), ("icon", "_0x83")],
                        Tree.Node("dataline_input", Token("dataline_input", 0x51),
                            [("name", "Klemme 2"), ("icon", "_0x83")]))));
        }
    }
}
