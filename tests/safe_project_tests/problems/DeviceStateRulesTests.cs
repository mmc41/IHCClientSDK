using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T051 — the four remaining DEVICE rows, and the two whose scope had to be settled before a line was written.
    ///
    /// <para><b>The gate this suite owes:</b> a test proving <c>dev-backup-missing</c> reports NO TERMINAL. Output
    /// terminals ship <c>backup="yes"</c> and input terminals declare no such attribute, so a rule that walked
    /// every backup-capable element would report most of a project — the row is about block variables alone, and
    /// <c>NoTerminalIsEverReportedForBackup</c> is that claim, asserted over a tree carrying both terminal
    /// kinds.</para>
    ///
    /// <para><b>Both scoped rows rest on the same shape</b> — the author has shown intent — and both are tested from
    /// the quiet side as well as the reporting one: a block that never marks a variable, and a product where
    /// nothing is configured, must produce nothing.</para>
    /// </summary>
    [TestFixture]
    public sealed class DeviceStateRulesTests
    {
        private static ProjectValidationResult Validate(Project project) =>
            new ProjectAppService(TestSetup.Settings).ValidateCategorized(project);

        private static int Count(Project project, string ruleId) =>
            Validate(project).Findings.Count(f => f.RuleId == ruleId);

        private static string Message(Project project, string ruleId) =>
            Validate(project).Findings.First(f => f.RuleId == ruleId).Message;

        // ── dev-write-to-read-only ──────────────────────────────────────────────────────────────────

        [Test]
        public void AnActionAssigningAReadOnlyVariableIsAnError()
        {
            Project readOnly = Block(access: "readonly");
            Project readWrite = Block(access: "readwrite");
            Project writeOnly = Block(access: "writeonly");

            Assert.Multiple(() =>
            {
                Assert.That(Count(readOnly, "dev-write-to-read-only"), Is.EqualTo(1));
                Assert.That(Validate(readOnly).Findings.Single(f => f.RuleId == "dev-write-to-read-only").Severity,
                    Is.EqualTo(ValidationSeverity.Error), "the catalogue rates this one an Error");
                Assert.That(Message(readOnly, "dev-write-to-read-only"), Does.Contain("Flag"));
                Assert.That(Count(readWrite, "dev-write-to-read-only"), Is.Zero);
                Assert.That(Count(writeOnly, "dev-write-to-read-only"), Is.Zero,
                    "writing to a write-only variable is what it is for");
            });
        }

        // ── dev-setting-default, and its threshold ──────────────────────────────────────────────────

        [Test]
        public void AnUntouchedSettingOnAConfiguredProductIsReported()
        {
            Project halfConfigured = Dimmer(storedSettings: 2);
            Project untouched = Dimmer(storedSettings: 0);
            Project fullyConfigured = Dimmer(storedSettings: 5);

            Assert.Multiple(() =>
            {
                Assert.That(Count(halfConfigured, "dev-setting-default"), Is.EqualTo(1),
                    "ONE finding for the product, with its untouched settings as related locations");
                Assert.That(Message(halfConfigured, "dev-setting-default"), Does.Contain("3").And.Contain("5"),
                    "how many are untouched, and out of how many");
                Assert.That(Count(untouched, "dev-setting-default"), Is.Zero,
                    "a freshly placed product has nothing configured and nothing forgotten");
                Assert.That(Count(fullyConfigured, "dev-setting-default"), Is.Zero);
            });
        }

        [Test]
        public void OneConfiguredSettingIsEnoughToMakeTheRestAnOmission()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("dev-setting-default"),
                out ProblemCatalogEntry entry), Is.True);
            DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == "MinimumConfiguredSettings");

            Assert.Multiple(() =>
            {
                Assert.That(declared.Value, Is.EqualTo(1));
                Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.Authored));
                Assert.That(declared.Evidence, Does.Contain("TODO"));
                Assert.That(Count(Dimmer(storedSettings: 1), "dev-setting-default"), Is.EqualTo(1),
                    "AT the threshold: one configured setting beside four untouched ones");
            });
        }

        // ── dev-backup-missing, including the gate's own test ───────────────────────────────────────

        [Test]
        public void AnUnmarkedStateVariableIsReportedOnlyWhereAnotherIsMarked()
        {
            Project contrast = BlockWithVariables(markedCount: 1, unmarkedCount: 2);
            Project noneMarked = BlockWithVariables(markedCount: 0, unmarkedCount: 3);
            Project allMarked = BlockWithVariables(markedCount: 3, unmarkedCount: 0);

            Assert.Multiple(() =>
            {
                Assert.That(Count(contrast, "dev-backup-missing"), Is.EqualTo(2),
                    "one per unmarked variable — each is separately repairable");
                Assert.That(Count(noneMarked, "dev-backup-missing"), Is.Zero,
                    "block variables default to unmarked, so an unmarked one says nothing on its own");
                Assert.That(Count(allMarked, "dev-backup-missing"), Is.Zero);
            });
        }

        /// <summary>
        /// THE GATE'S OWN TEST: no terminal is ever reported. The tree carries an output terminal shipping
        /// <c>backup="yes"</c> and an input terminal with no such attribute, beside a block that DOES report — so
        /// the rule is demonstrably running and demonstrably not looking at terminals.
        /// </summary>
        [Test]
        public void NoTerminalIsEverReportedForBackup()
        {
            Project project = TerminalsAndBlock();
            ProjectValidationFinding[] backup =
                [.. Validate(project).Findings.Where(f => f.RuleId == "dev-backup-missing")];
            string[] terminalIds =
                [.. project.Root.DescendantsAndSelf()
                    .Where(e => e.Tag is "dataline_output" or "dataline_input" or "airlink_relay")
                    .Select(e => e.GetAttribute("id")!)];

            Assert.Multiple(() =>
            {
                Assert.That(terminalIds, Is.Not.Empty, "sanity: the tree really carries terminals");
                Assert.That(backup, Is.Not.Empty, "sanity: the rule really ran and found the block's variable");
                Assert.That(backup.Where(f => terminalIds.Contains(f.Locator)), Is.Empty,
                    "an output terminal ships backup=yes and an input declares no such attribute — a walk over "
                    + "every backup-capable element would report most of a project");
            });
        }

        // ── dev-inivalue-overwritten ────────────────────────────────────────────────────────────────

        [Test]
        public void APowerUpProgramAssigningAStoredInitialValueIsReported()
        {
            Project overwritten = PowerUp(initialValue: "on", powerUp: true);
            Project defaulted = PowerUp(initialValue: null, powerUp: true);
            Project ordinary = PowerUp(initialValue: "on", powerUp: false);

            Assert.Multiple(() =>
            {
                Assert.That(Count(overwritten, "dev-inivalue-overwritten"), Is.EqualTo(1),
                    "the shape the vendor error fixture carries: a flag with inivalue=on, set again at powerup");
                Assert.That(Message(overwritten, "dev-inivalue-overwritten"), Does.Contain("on"));
                Assert.That(Count(defaulted, "dev-inivalue-overwritten"), Is.Zero,
                    "a variable at its DEFAULT initial value stores no inivalue — the canonicalizer elides it — "
                    + "and overwriting a default is not this row");
                Assert.That(Count(ordinary, "dev-inivalue-overwritten"), Is.Zero,
                    "an ordinary triggered program is not 'on every start'");
            });
        }

        // ── dev-inivalue-out-of-range ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Both ends of the percentage range, and both boundaries. An initial value no physical unit can reach —
        /// 150 % relative humidity — is carried, rendered and shipped to the controller without a word from any
        /// layer of the vendor tool.
        /// </summary>
        [Test]
        public void APercentInitialValueOutsideItsRangeIsReportedAtBothEnds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Percent("resource_humidity_level", "150"), "dev-inivalue-out-of-range"),
                    Is.EqualTo(1), "above the maximum");
                Assert.That(Count(Percent("resource_humidity_level", "-1"), "dev-inivalue-out-of-range"),
                    Is.EqualTo(1), "below the minimum");
                Assert.That(Count(Percent("resource_humidity_level", "100"), "dev-inivalue-out-of-range"),
                    Is.Zero, "AT the maximum");
                Assert.That(Count(Percent("resource_humidity_level", "0"), "dev-inivalue-out-of-range"),
                    Is.Zero, "AT the minimum");
                Assert.That(Count(Percent("resource_light_level", "150"), "dev-inivalue-out-of-range"),
                    Is.EqualTo(1), "the second percent-typed kind reports too");
            });
        }

        /// <summary>
        /// The value prints EXACTLY as the file carries it, decimals and all. That is why the slot is
        /// <c>AttributeValue</c> and not <c>Integer</c>: the measured case is
        /// <c>resource_humidity_level inivalue="150.00"</c>, which loads, renders verbatim and survives a plain
        /// resave — and an <c>Integer</c> slot would silently reformat it to <c>150</c>, quietly disagreeing with
        /// the bytes the reader is being asked to repair.
        /// </summary>
        [Test]
        public void TheReportedValueIsWhatTheFileCarriesIncludingItsDecimals()
        {
            Assert.That(Message(Percent("resource_humidity_level", "150.00"), "dev-inivalue-out-of-range"),
                Is.EqualTo("Startværdien 150.00 på 'Fugtighed' er uden for det gyldige område 0-100."));
        }

        /// <summary>
        /// THE SCOPE, which is the whole risk here. The row is deliberately PERCENT-ONLY: the two kinds whose
        /// 0–100 range the format specification records. Its sibling <c>resource_light</c> is a lux value on a
        /// 0–60,000 range and is emphatically not a percent kind, so reporting it against 0–100 would be wrong on
        /// every well-formed project that carries one; a counter's negative value was measured carried and no
        /// source calls it illegal; and a timer stores no <c>inivalue</c> at all — its value lives in
        /// <c>hour</c>/<c>minute</c>/<c>second</c>.
        /// </summary>
        [Test]
        public void OnlyThePercentTypedKindsAreInScope()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Percent("resource_light", "5000"), "dev-inivalue-out-of-range"), Is.Zero,
                    "a lux value of 5000 is ordinary — that kind's range is 0-60,000");
                Assert.That(Count(Percent("resource_counter", "-5"), "dev-inivalue-out-of-range"), Is.Zero,
                    "no source states that a negative count is illegal");
                Assert.That(Count(Percent("resource_integer", "40000"), "dev-inivalue-out-of-range"), Is.Zero,
                    "the integer range is available but this row is scoped to percent (D06)");
            });
        }

        /// <summary>
        /// A value arithmetic cannot read is not a range violation: there is nothing to compare. A resource with
        /// no <c>inivalue</c> at all is likewise untouched — that is the default state every file is full of.
        /// </summary>
        [Test]
        public void AnUnreadableOrAbsentInitialValueIsNotReported()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(Percent("resource_humidity_level", "høj"), "dev-inivalue-out-of-range"),
                    Is.Zero, "the grammar admits it; arithmetic cannot read it, so this row has nothing to say");
                Assert.That(Count(Percent("resource_humidity_level", null), "dev-inivalue-out-of-range"),
                    Is.Zero, "and a resource left at its default carries no inivalue at all");
            });
        }

        /// <summary>
        /// Both bounds are declared, and both grade <c>VendorRecommendation</c> rather than
        /// <c>VendorDocumented</c>: vendor help states the range, and the same source measured that NOTHING in
        /// the load, display, commit or save path enforces it. That is guidance, not a limit — which is also why
        /// the row is a Warning.
        /// </summary>
        [Test]
        public void ThePercentBoundsAreDeclaredAsUnenforcedVendorGuidance()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("dev-inivalue-out-of-range"),
                out ProblemCatalogEntry entry), Is.True);

            Assert.Multiple(() =>
            {
                foreach (string name in new[] { "PercentMinimum", "PercentMaximum" })
                {
                    DeclaredThreshold declared = entry.Thresholds.Single(t => t.Name == name);
                    Assert.That(declared.Confidence, Is.EqualTo(ThresholdConfidence.VendorRecommendation), name);
                    Assert.That(declared.Evidence, Does.Contain("not enforced"), name);
                }

                Assert.That(entry.Slots.Select(s => s.Name),
                    Is.EqualTo(new[] { "value", "variable", "minimum", "maximum" }).AsCollection,
                    "declared order is the template's first-appearance order: it opens on {value}");
            });
        }

        // ── backup-retained-count ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// The retention budget is a controller-side ration, and this number is what will be measured against it
        /// at upload. ONE finding for the project: the count is the fact, and anchoring per resource would nag
        /// about each of them separately.
        /// </summary>
        [Test]
        public void TheRetainedResourceCountIsReportedOnceForTheProject()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Count(BlockWithVariables(markedCount: 3, unmarkedCount: 0), "backup-retained-count"),
                    Is.EqualTo(1), "one finding, however many resources are marked");
                Assert.That(Message(BlockWithVariables(markedCount: 3, unmarkedCount: 0), "backup-retained-count"),
                    Does.Contain("3"), "and the count is the fact it carries");
                Assert.That(Message(BlockWithVariables(markedCount: 1, unmarkedCount: 2), "backup-retained-count"),
                    Does.Contain("1"), "unmarked resources are not part of the budget");
                Assert.That(Count(BlockWithVariables(markedCount: 0, unmarkedCount: 3), "backup-retained-count"),
                    Is.Zero, "a project asking for nothing to be retained has no budget to report");
            });
        }

        /// <summary>
        /// NO VERDICT AND NO THRESHOLD, deliberately. The controller's retention ceiling is not established
        /// anywhere in this row's source, so `RequiresControllerLimits` is NOT set and the row reports the count
        /// alone — it states a fact the reader can weigh, rather than a limit the SDK cannot cite.
        ///
        /// <para>That is also why it is Information: exceeding the ration is a different condition, and this row
        /// is not claiming the project does.</para>
        /// </summary>
        [Test]
        public void TheRetainedCountCarriesNoCeilingAndNoVerdict()
        {
            Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode("backup-retained-count"),
                out ProblemCatalogEntry entry), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entry.Thresholds, Is.Empty, "no number to compare against");
                Assert.That(entry.RequiresControllerLimits, Is.False,
                    "and no controller context asked for, because none would be used");
                Assert.That(Validate(BlockWithVariables(markedCount: 2, unmarkedCount: 0)).Findings
                    .Single(f => f.RuleId == "backup-retained-count").Severity,
                    Is.EqualTo(ValidationSeverity.Info));
            });
        }

        /// <summary>
        /// THE COUNT IS OVER <c>resource_*</c> ELEMENTS, AND A TERMINAL IS NOT ONE. An output terminal ships
        /// <c>backup="yes"</c> too, but <c>dataline_output</c> is not a resource element and is not counted.
        ///
        /// <para>Whether a terminal's retained value consumes the same controller-side ration is NOT established
        /// by this row's source, which says <c>resource_*</c> in as many words. Counting terminals would be
        /// asserting an equivalence nobody measured; the row counts what the source scopes it to, and this test
        /// records the boundary so a later widening is a decision rather than a drift.</para>
        /// </summary>
        [Test]
        public void OnlyResourceElementsCountTowardsTheRetainedBudget()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Message(TerminalsAndBlock(), "backup-retained-count"), Does.Contain("1"),
                    "the marked block flag alone — the marked output TERMINAL is not a resource element");
                Assert.That(Count(TerminalsAndBlock(), "dev-backup-missing"), Is.EqualTo(1),
                    "and the scoped row still reports the unmarked block variable, reading the same attribute "
                    + "for a different purpose");
            });
        }

        // ── tree builders ───────────────────────────────────────────────────────────────────────────

        private static string Token(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static Project InLocality(params ProjectElement[] contents) =>
            Tree.WithRoot(Tree.Node("groups", Token("groups", 0x20), [("name", "L")],
                Tree.Node("group", Token("group", 0x21), [("name", "Stue")], contents)));

        private static ProjectElement BlockShell(int at, string name, ProjectElement[] internals, ProjectElement[] programs) =>
            Tree.Node("functionblock", Token("functionblock", at), [("name", name)],
                Tree.Node("inputs", Token("inputs", at + 1), [("name", "Input")]),
                Tree.Node("outputs", Token("outputs", at + 2), [("name", "Output")]),
                Tree.Node("settings", Token("settings", at + 3), [("name", "Indstillinger")]),
                Tree.Node("internalsettings", Token("internalsettings", at + 4), [("name", "Interne")], internals),
                Tree.Node("programs", Token("programs", at + 5), [("name", "Programmer")], programs));

        /// <summary>
        /// A block holding one resource of the given kind carrying the given initial value — or none at all when
        /// <paramref name="value"/> is null, which is the state every default-valued resource is in.
        /// </summary>
        /// <param name="tag">The resource element's tag.</param>
        /// <param name="value">The <c>inivalue</c> to store, or null to store none.</param>
        private static Project Percent(string tag, string? value) =>
            InLocality(BlockShell(0x70, "Blok",
                [Tree.Node(tag, Token(tag, 0x80),
                    value is null
                        ? [("name", "Fugtighed")]
                        : [("name", "Fugtighed"), ("inivalue", value)])],
                []));

        /// <summary>A block whose single program assigns one flag carrying the given access.</summary>
        private static Project Block(string access) =>
            InLocality(BlockShell(0x70, "Blok",
                [Tree.Node("resource_flag", Token("resource_flag", 0x80),
                    [("name", "Flag"), ("access", access)])],
                [Program(0x90, targetAt: 0x80, powerUp: false)]));

        /// <summary>A block with the given number of marked and unmarked state variables.</summary>
        private static Project BlockWithVariables(int markedCount, int unmarkedCount)
        {
            ImmutableArray<ProjectElement>.Builder variables = ImmutableArray.CreateBuilder<ProjectElement>();
            int at = 0x80;
            for (int i = 0; i < markedCount; i++)
            {
                variables.Add(Tree.Node("resource_flag", Token("resource_flag", at++),
                    [("name", $"Gemt {i}"), ("backup", "yes")]));
            }

            for (int i = 0; i < unmarkedCount; i++)
            {
                variables.Add(Tree.Node("resource_counter", Token("resource_counter", at++),
                    [("name", $"Tæller {i}")]));
            }

            return InLocality(BlockShell(0x70, "Blok", [.. variables], []));
        }

        /// <summary>Two terminals — one shipping backup=yes, one declaring none — beside a reporting block.</summary>
        private static Project TerminalsAndBlock() =>
            InLocality(
                Tree.Node("product_dataline", Token("product_dataline", 0x51),
                    [("product_identifier", "_0x2202"), ("name", "Produkt")],
                    Tree.Node("dataline_output", Token("dataline_output", 0x52),
                        [("name", "Udgang"), ("backup", "yes")]),
                    Tree.Node("dataline_input", Token("dataline_input", 0x53), [("name", "Tryk")])),
                BlockShell(0x70, "Blok",
                    [
                        Tree.Node("resource_flag", Token("resource_flag", 0x80),
                            [("name", "Gemt"), ("backup", "yes")]),
                        Tree.Node("resource_flag", Token("resource_flag", 0x81), [("name", "Ikke gemt")]),
                    ],
                    []));

        /// <summary>A dimmer product whose first <paramref name="storedSettings"/> settings store a value.</summary>
        private static Project Dimmer(int storedSettings)
        {
            string[] tags =
            [
                "dimmer_setting_minimum_value", "dimmer_setting_maximum_value", "dimmer_setting_fade_rate_up",
                "dimmer_setting_fade_rate_down", "dimmer_setting_dimming_rate",
            ];
            ImmutableArray<ProjectElement>.Builder settings = ImmutableArray.CreateBuilder<ProjectElement>();
            for (int i = 0; i < tags.Length; i++)
            {
                settings.Add(Tree.Node(tags[i], Token(tags[i], 0x80 + i),
                    i < storedSettings ? [("value", "42")] : []));
            }

            return InLocality(Tree.Node("product_airlink", Token("product_airlink", 0x60),
                [("product_identifier", "_0x4304"), ("name", "Trådløs dæmper"), ("serialnumber", "_0xaa11")],
                Tree.Node("airlink_dimming", Token("airlink_dimming", 0x61),
                    [("name", "Dæmp"), ("address_channel", "_0x1")]),
                Tree.Node("dimmer_settings", Token("dimmer_settings", 0x70), [("name", "Indstillinger")],
                    [.. settings])));
        }

        /// <summary>A block whose program — power-up or ordinary — assigns one flag with the given initial value.</summary>
        private static Project PowerUp(string? initialValue, bool powerUp) =>
            InLocality(BlockShell(0x70, "Blok",
                [Tree.Node("resource_flag", Token("resource_flag", 0x80),
                    initialValue is null
                        ? [("name", "Startværdi")]
                        : [("name", "Startværdi"), ("inivalue", initialValue)])],
                [Program(0x90, targetAt: 0x80, powerUp: powerUp)]));

        private static ProjectElement Program(int at, int targetAt, bool powerUp) =>
            Tree.Node("program_simple", Token("program_simple", at), [("name", "Program")],
                Tree.Node("events", Token("events", at + 1), [("name", "Hændelser")],
                    powerUp
                        ? Tree.Node("event_power", Token("event_power", at + 2), [("name", "Powerup")])
                        : Tree.Node("event", Token("event", at + 2),
                            [("name", "%P -> ON"), ("link1", Token("resource_flag", targetAt)), ("method", "_0xa")])),
                Tree.Node("actions", Token("actions", at + 3), [("name", "Kommandoer"), ("type", "_0x2")],
                    Tree.Node("action", Token("action", at + 4),
                        [("name", "%P = ON"), ("link1", Token("resource_flag", targetAt)), ("method", "_0xa")])));
    }
}
