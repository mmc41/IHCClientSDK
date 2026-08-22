#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ihc.Tests.Shared;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The permanent generality sweep (reportgenerality T001): the whole report contract was pinned by TWO
    /// fixture projects, so nothing said what the generator does with the other 25. This runs every
    /// <c>testdata/projects/**/*.vis</c> through all 3 kinds × 2 modes × 2 formats and asserts in two layers,
    /// because either one alone is worthless:
    /// <list type="bullet">
    ///   <item><b>envelope</b> — generation completes, the bytes are non-empty and the document is
    ///   well-formed for its format. This layer would pass with every project-specific row silently dropped,
    ///   which is the exact failure the sweep exists to catch.</item>
    ///   <item><b>semantic (model-to-report)</b> — what the PROJECT holds must reach the report: every
    ///   end-user-flagged product in the functions report, every dataline terminal in the installation
    ///   cross-reference, every function block in the FB report.</item>
    /// </list>
    /// Two known gaps make a semantic assertion fail today, so they are stated INVERTED and named for their
    /// finding (<c>…_AreCurrentlyDropped_G1/G2</c>): the sweep documents the gap instead of hiding it, and
    /// each flips to a positive assertion when its ruling lands. Both inverted assertions are paired with an
    /// armed-detector test, so neither can quietly go vacuous if the corpus changes.
    /// <para>Semantics are asserted against the <b>Full</b> text rendering: it is the superset of Standard,
    /// and it carries the <c>(ID _0x…)</c> chips that make "this element's row was emitted" an exact,
    /// collision-free probe rather than a name substring search.</para>
    /// </summary>
    public class ReportGeneralitySweepTests
    {
        private static readonly string ProjectDir = TestData.PathOf("projects");

        private static readonly ReportKind[] Kinds =
            [ReportKind.Functions, ReportKind.Installation, ReportKind.FunctionBlocks];

        private static readonly ReportMode[] Modes = [ReportMode.Standard, ReportMode.Full];

        private static readonly string[] Formats = [ReportMimeTypes.PlainText, ReportMimeTypes.Html];

        /// <summary>The function-block containers the FB report renders, in the fixed order it renders them —
        /// so the n-th rendered section row is the n-th of these the block actually declares.</summary>
        private static readonly string[] SectionOrder = ["inputs", "outputs", "settings", "internalsettings", "programs"];

        /// <summary>The variable types the FB report's sections render one row each for today. Mirrored here
        /// rather than read from the builder: it is the MODEL side of a model-to-report assertion.</summary>
        private static readonly string[] RenderedVariableTags =
        [
            "resource_input", "resource_output", "resource_scene",
            "resource_timer", "resource_time", "resource_timertime", "resource_counter", "resource_integer",
            "resource_enum", "resource_date", "resource_weekday", "resource_flag", "resource_temperature",
            "resource_light_level", "resource_floating_point",
        ];

        /// <summary>
        /// The register-C1 variable types — the ones the vendor's own report lost, which this report
        /// renders in Full mode only (finding G2 / RL-4). Scanned
        /// case-sensitively over BOTH cases (decision D-f): the four energy elements are uppercase DTD
        /// element names, and a lowercase-anchored scan silently under-reports the gap as three types.
        /// </summary>
        private static readonly string[] RegisterC1VariableTags =
            ["resource_holiday", "resource_humidity_level", "resource_light", "kW", "kWh", "W", "Wh"];

        // One service and one loaded Project per fixture for the whole sweep: 324 envelope cases over a
        // 27-fixture corpus would otherwise re-parse a 236 KB project dozens of times. Projects are
        // immutable and the assembly is [NonParallelizable], so sharing them is safe.
        private static readonly Lazy<ProjectAppService> Service =
            new(() => new ProjectAppService(TestSetup.Settings, new BuiltInCatalog(), ReportOracles.Clock()));

        private static readonly ConcurrentDictionary<string, Project> Loaded = new(StringComparer.Ordinal);

        // ----- the case matrix -----

        /// <summary>Every fixture in the corpus, as a testdata-relative path with forward slashes so the
        /// generated test names are identical on every OS.</summary>
        private static IEnumerable<string> Fixtures() =>
            Directory.EnumerateFiles(ProjectDir, "*.vis", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(ProjectDir, path).Replace('\\', '/'))
                .OrderBy(name => name, StringComparer.Ordinal);

        private static IEnumerable<object[]> EnvelopeCases() =>
            from fixture in Fixtures()
            from kind in Kinds
            from mode in Modes
            from format in Formats
            select new object[] { fixture, kind, mode, format };

        [Test]
        public void TheSweep_CoversTheWholeCorpus_InEveryCombination()
        {
            int fixtures = Fixtures().Count();
            Assert.Multiple(() =>
            {
                Assert.That(fixtures, Is.GreaterThan(0), "the sweep found no .vis fixtures — testdata is not being copied");
                Assert.That(EnvelopeCases().Count(), Is.EqualTo(fixtures * 12),
                    "every fixture must be swept in all 3 kinds × 2 modes × 2 formats");
            });
        }

        // ----- layer (a): envelope -----

        [TestCaseSource(nameof(EnvelopeCases))]
        public async Task EveryFixture_GeneratesAWellFormedReport(
            string fixture, ReportKind kind, ReportMode mode, string mimeType)
        {
            using var output = new MemoryStream();

            await Service.Value.GenerateReport(Load(fixture), kind, mode, mimeType, output);

            byte[] bytes = output.ToArray();
            Assert.That(bytes, Is.Not.Empty, $"{fixture}: {kind}/{mode}/{mimeType} generated no bytes");
            string report = Encoding.UTF8.GetString(bytes);
            if (mimeType == ReportMimeTypes.PlainText)
            {
                AssertTextEnvelope(report, kind, fixture);
            }
            else
            {
                AssertHtmlEnvelope(report, kind, fixture);
            }
        }

        private static void AssertTextEnvelope(string report, ReportKind kind, string fixture)
        {
            string title = ReportTitles.For(kind);
            Assert.That(report, Does.StartWith($"IHC OpenVisual\n\n{title}\n{new string('=', title.Length)}\n"),
                $"{fixture}: the text report must open with the banner and the underlined title");
        }

        private static void AssertHtmlEnvelope(string report, ReportKind kind, string fixture)
        {
            Assert.Multiple(() =>
            {
                Assert.That(report, Does.StartWith("<!doctype html>\n<html lang=\"da\">\n"),
                    $"{fixture}: the HTML report must be a complete document");
                Assert.That(report, Does.Contain($"<title>{ReportTitles.For(kind)} &mdash; IHC OpenVisual</title>"),
                    $"{fixture}: the HTML report must carry its kind's title");
                Assert.That(report, Does.EndWith("</body>\n</html>\n"),
                    $"{fixture}: the HTML report must close its document");
                Assert.That(ReportProbe.Occurrences(report, "<table"), Is.EqualTo(ReportProbe.Occurrences(report, "</table>")),
                    $"{fixture}: unbalanced <table> elements in the {kind} report");
                Assert.That(ReportProbe.Occurrences(report, "<ul"), Is.EqualTo(ReportProbe.Occurrences(report, "</ul>")),
                    $"{fixture}: unbalanced <ul> elements in the {kind} report");
            });
        }

        // ----- layer (b): semantic, model-to-report -----

        [TestCaseSource(nameof(Fixtures))]
        public async Task EveryEndUserProduct_AppearsInTheFunctionsReport(string fixture)
        {
            Project project = Load(fixture);
            IReadOnlyDictionary<ProjectElement, ProjectElement> parents = Parents(project);
            string report = await FullText(fixture, ReportKind.Functions);

            List<ProjectElement> products = project.Root.DescendantsAndSelf()
                .Where(e => e.Tag is "product_dataline" or "product_airlink")
                .Where(e => project.View(e).EnduserReport && HasLocality(e, parents))
                .ToList();

            Assert.Multiple(() =>
            {
                foreach (ProjectElement product in products)
                {
                    AssertRendered(report, product, isRendered: true, fixture,
                        "an end-user-flagged product must reach the functions report");
                }
            });
        }

        [TestCaseSource(nameof(Fixtures))]
        public async Task EveryDatalineTerminal_AppearsInTheInstallationCrossReference(string fixture)
        {
            Project project = Load(fixture);
            IReadOnlyDictionary<ProjectElement, ProjectElement> parents = Parents(project);
            string report = await FullText(fixture, ReportKind.Installation);
            string crossReference = ReportProbe.CrossReferenceSection(report);

            List<ProjectElement> inputs = Terminals(project, "dataline_input");
            List<ProjectElement> outputs = Terminals(project, "dataline_output");

            Assert.Multiple(() =>
            {
                Assert.That(ReportProbe.TableRowCount(report, "Datalinie indgange"), Is.EqualTo(inputs.Count),
                    $"{fixture}: the input cross-reference must carry one row per dataline_input");
                Assert.That(ReportProbe.TableRowCount(report, "Datalinie udgange"), Is.EqualTo(outputs.Count),
                    $"{fixture}: the output cross-reference must carry one row per dataline_output");
                foreach (ProjectElement terminal in inputs.Concat(outputs)
                    .Where(t => NearestProductTag(t, parents) is not null))
                {
                    string name = ReportText.SingleLine(terminal.GetAttribute("name")).Trim();
                    Assert.That(crossReference, Does.Contain(name),
                        $"{fixture}: terminal '{name}' is missing from the installation cross-reference");
                }
            });
        }

        [TestCaseSource(nameof(Fixtures))]
        public async Task EveryFunctionBlock_AppearsInTheFunctionBlockReport(string fixture)
        {
            Project project = Load(fixture);
            string report = await FullText(fixture, ReportKind.FunctionBlocks);

            List<ProjectElement> blocks = project.Root.DescendantsAndSelf()
                .Where(e => e.Tag == "functionblock")
                .ToList();

            Assert.Multiple(() =>
            {
                foreach (ProjectElement block in blocks)
                {
                    AssertRendered(report, block, isRendered: true, fixture,
                        "every function block must reach the function-block report");
                }
            });
        }

        // ----- the two witnessed gaps, stated inverted -----

        [TestCaseSource(nameof(Fixtures))]
        public async Task AirlinkTerminals_ReachTheEndUserReport_FullOnlyBeyondTheInputs_G1(string fixture)
        {
            Project project = Load(fixture);
            string full = await Text(fixture, ReportKind.Functions, ReportMode.Full);
            string standard = await Text(fixture, ReportKind.Functions, ReportMode.Standard);

            Assert.Multiple(() =>
            {
                foreach (ProjectElement product in EndUserAirlinkProducts(project))
                {
                    foreach (ProjectElement terminal in product.Children.Where(c => c.Tag.StartsWith("airlink_", StringComparison.Ordinal)))
                    {
                        bool isInput = terminal.Tag == "airlink_input";
                        AssertRendered(full, terminal, isRendered: true, fixture,
                            "G1: EVERY airlink terminal kind reaches the end-user report in Full mode — a "
                            + "product whose terminals are all relay/shutter/dimming used to print as a bare "
                            + "name with no children at all");
                        // Standard strips the id chips, so membership there is an exact row-line match.
                        Assert.That(
                            ReportProbe.HasTreeLine(standard, 2, ReportText.SingleLine(terminal.GetAttribute("name"))),
                            Is.EqualTo(isInput),
                            $"{fixture}: <{terminal.Tag}> '{terminal.GetAttribute("name")}' — " + (isInput
                                ? "an airlink input is vendor-parity content, so Standard keeps it"
                                : "C-3: Standard is the vendor-parity surface, so the kinds the vendor's "
                                  + "report loses stay out of it"));
                    }
                }
            });
        }

        [Test]
        public void TheCorpusWitnesses_TheFullOnlyAirlinkTerminals_G1()
        {
            int dropped = Fixtures().Sum(fixture => EndUserAirlinkProducts(Load(fixture))
                .SelectMany(p => p.Children)
                .Count(c => c.Tag.StartsWith("airlink_", StringComparison.Ordinal) && c.Tag != "airlink_input"));

            Assert.That(dropped, Is.GreaterThan(0),
                "no fixture carries a non-input airlink terminal under an end-user product, so the G1 "
                + "assertion above is vacuous — the corpus lost its witness");
        }

        // Counted, not name-matched: a variable's name also arrives in the report as a program statement's
        // substituted %P/%S operand, and the same name is reused across a block's four sections — so a probe
        // by name can never see whether the variable's own row is there. One row per admitted child is exact
        // either way, and it reads the same in both modes.
        [TestCaseSource(nameof(Fixtures))]
        public async Task VariableRows_AreRenderedPerSection_TheRegisterC1TypesInFullOnly_G2(string fixture)
        {
            Project project = Load(fixture);
            string full = await Text(fixture, ReportKind.FunctionBlocks, ReportMode.Full);
            string standard = await Text(fixture, ReportKind.FunctionBlocks, ReportMode.Standard);

            List<ProjectElement> blocks = [.. FunctionBlocks(project)];
            var byName = blocks
                .GroupBy(b => ReportText.Collapse(b.GetAttribute("name")), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            Assert.Multiple(() =>
            {
                foreach (ProjectElement block in blocks)
                {
                    string name = ReportText.Collapse(block.GetAttribute("name"));
                    List<ProjectElement> sections = RenderedSections(block);
                    int[] inFull = ReportProbe.SectionChildCounts(
                        full, name + " " + ReportProbe.Chip(block.GetAttribute("id")!));
                    Assert.That(inFull, Has.Length.EqualTo(sections.Count),
                        $"{fixture}: block '{name}' must render one row per declared section");

                    // Standard strips the id chip, so a block is addressable there only by its bare name —
                    // and a project may hold several blocks of the same name (project3 has three "Tom blok").
                    // Those are skipped rather than asserted against the wrong block's rows; the Standard
                    // side is in any case byte-pinned by the std-* oracles.
                    int[] inStandard = byName[name] == 1
                        ? ReportProbe.SectionChildCounts(standard, name)
                        : [];

                    for (int section = 0; section < sections.Count && section < inFull.Length; section++)
                    {
                        if (sections[section].Tag == "programs")
                        {
                            continue;   // its children are the program tree, not variable rows
                        }
                        int common = Count(sections[section], RenderedVariableTags);
                        int fullOnly = Count(sections[section], RegisterC1VariableTags);
                        Assert.That(inFull[section], Is.EqualTo(common + fullOnly),
                            $"{fixture}: G2 — <{sections[section].Tag}> of block '{name}' renders every "
                            + $"variable it declares in Full, including its {fullOnly} register-C1 one(s)");
                        if (section < inStandard.Length)
                        {
                            Assert.That(inStandard[section], Is.EqualTo(common),
                                $"{fixture}: C-3 — Standard is the vendor-parity surface, so the {fullOnly} "
                                + "register-C1 variable(s) stay out of it");
                        }
                    }
                }
            });
        }

        [Test]
        public void TheCorpusWitnesses_TheFullOnlyVariableTypes_G2()
        {
            List<string> dropped = Fixtures()
                .SelectMany(fixture => FunctionBlocks(Load(fixture)))
                .SelectMany(RenderedSections)
                .SelectMany(section => section.Children)
                .Select(child => child.Tag)
                .Where(tag => RegisterC1VariableTags.Contains(tag, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(dropped, Is.Not.Empty,
                    "no fixture carries a register-C1 variable inside a function block, so the G2 assertion "
                    + "above is vacuous — the corpus lost its witness");
                Assert.That(UniquelyNamedBlocksCarryingRegisterC1(), Is.Not.Empty,
                    "every register-C1 variable in the corpus sits in a block sharing its name with another, "
                    + "so the STANDARD half of the G2 assertion is skipped everywhere — it would pass "
                    + "vacuously");
            });
        }

        // ----- model queries -----

        // The blocks the Standard half of the G2 assertion can actually address: a register-C1 variable
        // inside a block whose name is unique in its fixture.
        private static List<string> UniquelyNamedBlocksCarryingRegisterC1() =>
        [
            .. from fixture in Fixtures()
               let project = Load(fixture)
               let blocks = FunctionBlocks(project).ToList()
               from block in blocks
               let name = ReportText.Collapse(block.GetAttribute("name"))
               where blocks.Count(b => ReportText.Collapse(b.GetAttribute("name")) == name) == 1
                  && RenderedSections(block).Any(section => Count(section, RegisterC1VariableTags) > 0)
               select fixture + ":" + name,
        ];

        private static IEnumerable<ProjectElement> FunctionBlocks(Project project) =>
            project.Root.DescendantsAndSelf().Where(e => e.Tag == "functionblock" && e.GetAttribute("id") is { Length: > 0 });

        /// <summary>The block's declared sections in render order — the model side of "the n-th rendered
        /// section row". A repeated container renders once, so only the first of a tag counts.</summary>
        private static List<ProjectElement> RenderedSections(ProjectElement block) =>
            SectionOrder
                .Select(tag => block.Children.FirstOrDefault(child => child.Tag == tag))
                .Where(section => section is not null)
                .Select(section => section!)
                .ToList();

        private static int Count(ProjectElement section, string[] tags) =>
            section.Children.Count(child => tags.Contains(child.Tag, StringComparer.Ordinal));

        private static IEnumerable<ProjectElement> EndUserAirlinkProducts(Project project) =>
            project.Root.DescendantsAndSelf()
                .Where(e => e.Tag == "product_airlink" && project.View(e).EnduserReport);

        private static List<ProjectElement> Terminals(Project project, string tag) =>
            project.Root.DescendantsAndSelf().Where(e => e.Tag == tag).ToList();

        private static IReadOnlyDictionary<ProjectElement, ProjectElement> Parents(Project project)
        {
            // Built here rather than borrowed from the reporting layer's own TreeIndex: the model side of a
            // model-to-report assertion must not be derived from the code under test.
            var parents = new Dictionary<ProjectElement, ProjectElement>(ReferenceEqualityComparer.Instance);
            var pending = new Stack<ProjectElement>();
            pending.Push(project.Root);
            while (pending.Count > 0)
            {
                ProjectElement element = pending.Pop();
                foreach (ProjectElement child in element.Children)
                {
                    parents[child] = element;
                    pending.Push(child);
                }
            }
            return parents;
        }

        private static bool HasLocality(ProjectElement element, IReadOnlyDictionary<ProjectElement, ProjectElement> parents) =>
            NearestAncestor(element, parents, tag => tag == "group") is not null;

        private static string? NearestProductTag(ProjectElement element, IReadOnlyDictionary<ProjectElement, ProjectElement> parents) =>
            NearestAncestor(element, parents, tag => tag.StartsWith("product_", StringComparison.Ordinal));

        private static string? NearestAncestor(ProjectElement element,
            IReadOnlyDictionary<ProjectElement, ProjectElement> parents, Func<string, bool> matches)
        {
            string? found = null;
            ProjectElement? current = element;
            while (found is null && parents.TryGetValue(current!, out ProjectElement? parent))
            {
                found = matches(parent.Tag) ? parent.Tag : null;
                current = parent;
            }
            return found;
        }

        // ----- report probes -----

        /// <summary>Asserts whether an element's row was emitted, probed by its Full-mode <c>(ID _0x…)</c>
        /// chip — an exact token, where a name substring can collide with any other row's text.</summary>
        private static void AssertRendered(string report, ProjectElement element, bool isRendered,
            string fixture, string because)
        {
            if (element.GetAttribute("id") is { Length: > 0 } id)
            {
                Assert.That(ReportProbe.Renders(report, id), Is.EqualTo(isRendered),
                    $"{fixture}: <{element.Tag}> '{ReportText.SingleLine(element.GetAttribute("name"))}' — {because}");
            }
        }

        // ----- fixture loading -----

        private static Project Load(string fixture) =>
            Loaded.GetOrAdd(fixture, name => Service.Value
                .Load(new MemoryStream(TestData.ReadBytes(Path.Combine("projects", name.Replace('/', Path.DirectorySeparatorChar)))))
                .GetAwaiter().GetResult());

        private static Task<string> FullText(string fixture, ReportKind kind) =>
            Text(fixture, kind, ReportMode.Full);

        private static async Task<string> Text(string fixture, ReportKind kind, ReportMode mode)
        {
            using var output = new MemoryStream();
            await Service.Value.GenerateReport(Load(fixture), kind, mode, ReportMimeTypes.PlainText, output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
