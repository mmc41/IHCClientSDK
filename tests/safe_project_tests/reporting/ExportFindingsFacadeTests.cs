using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Ihc.Vis.Catalog;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The facade door: <c>ProjectAppService.ExportFindings</c>, its four overloads, and the format constant
    /// that names the file.
    ///
    /// <para><b>Why the facade at all.</b> A frontend must not compose a <see cref="ValidationProfile"/>, must
    /// not name <c>IWholeProjectValidator</c> and must not hold the rule set. The same reasoning that put
    /// <c>GenerateReport</c> here puts this here, and the overload shape mirrors it deliberately: one
    /// generation path, two sinks, and the sink runs only after generation succeeds so a failure never leaves
    /// a truncated file.</para>
    ///
    /// <para><b>Why FOUR and not two.</b> The two project overloads run validation themselves — the ordinary
    /// door. The two sequence overloads take a caller's own list and emit it verbatim, which is what lets a
    /// host export exactly what its panel is showing. Without the sequence+path pair a host would have to
    /// open and manage the stream itself for no reason, since it wants a file.</para>
    /// </summary>
    [TestFixture]
    public sealed class ExportFindingsFacadeTests
    {
        /// <summary>
        /// The instant every export in this fixture is stamped with.
        /// <para>FIXED, because an export's <c>generated</c> attribute is taken from the service's clock to the
        /// second, and two exports of one payload are only byte-identical if they share it. Nothing here asserts
        /// on what the timestamp says, so pinning it costs no coverage — see
        /// <see cref="TwoExportsDifferOnlyByTheirGeneratedStamp"/>, which is the mechanism stated as a test.</para>
        /// </summary>
        private static readonly DateTimeOffset FixedNow = new(2026, 9, 5, 12, 0, 0, TimeSpan.FromHours(2));

        /// <summary>Materialized ONCE: the catalog is the expensive half of building a service, and every test
        /// here wants the same one.</summary>
        private static readonly ICatalog Catalog = new BuiltInCatalog();

        private static ProjectAppService App() =>
            new(TestSetup.Settings, Catalog, new FakeTimeProvider(FixedNow));

        private static Project Corpus() => ValidationCharacterizationTests.Corpus
            .First(c => c.Case == "fixture/Project6-Errors").Build();

        private static async Task<string> ExportToText(Func<Stream, Task> export)
        {
            using var stream = new MemoryStream();
            await export(stream);
            return ProjectFile.Encoding.GetString(stream.ToArray());
        }

        private static string Code(string line) => line.Split(" code=\"")[1].Split('"')[0];

        // ----- the format constant -----

        /// <summary>
        /// The findings export declares its OWN format, and the report mapping is left alone.
        /// <para>
        /// The two belong to different contracts and the separation is the assertion: <c>ReportMimeTypes</c>
        /// publishes what <c>GenerateReport</c> accepts, and <c>GenerateReport</c> rejects
        /// <c>application/xml</c> — so a member for it there would be the type contradicting its own summary,
        /// and a caller who trusted the class name would get an exception. <c>ExportFindings</c> takes no
        /// mimetype at all, so its format is a declaration rather than a second lookup table.
        /// </para>
        /// </summary>
        [Test]
        public void TheFindingsExportDeclaresItsOwnFormat()
        {
            Assert.Multiple(() =>
            {
                Assert.That(FindingExportFormat.MimeType, Is.EqualTo("application/xml"));
                Assert.That(FindingExportFormat.FileExtension, Is.EqualTo("xml"));

                // The report mapping still answers for the two formats it is about, and still defaults the rest
                // to HTML — the behaviour a host naming a report file depends on.
                Assert.That(ReportMimeTypes.FileExtensionFor(ReportMimeTypes.PlainText), Is.EqualTo("txt"));
                Assert.That(ReportMimeTypes.FileExtensionFor(ReportMimeTypes.Html), Is.EqualTo("html"));
                Assert.That(
                    ReportMimeTypes.FileExtensionFor("application/octet-stream"), Is.EqualTo("html"),
                    "the everything-else default is unchanged");
                Assert.That(
                    ReportMimeTypes.FileExtensionFor(FindingExportFormat.MimeType), Is.EqualTo("html"),
                    "and it does NOT special-case the findings mimetype: that value never reaches this mapping, "
                    + "because a findings export is asked for by its own door and never by a format argument");
            });
        }

        // ----- the project overloads -----

        /// <summary>The stream overload writes a complete document for a real corpus project.</summary>
        [Test]
        public async Task TheProjectStreamOverloadWritesACompleteDocument()
        {
            string text = await ExportToText(s => App().ExportFindings(Corpus(), s));

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n"));
                Assert.That(text, Does.EndWith("</ihc_project_findings>\r\n"));
                Assert.That(FindingExportProbe.FindingLines(text), Is.Not.Empty, "this fixture is the errors one; it has findings");
            });
        }

        /// <summary>The path overload writes the same bytes to a file, overwriting what was there.</summary>
        [Test]
        public async Task ThePathOverloadWritesTheSameBytesAndOverwrites()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "export-findings-overwrite.xml");
            await File.WriteAllTextAsync(path, "stale content that must not survive");

            ProjectAppService app = App();
            Project project = Corpus();
            await app.ExportFindings(project, path);
            byte[] viaPath = await File.ReadAllBytesAsync(path);

            using var stream = new MemoryStream();
            await app.ExportFindings(project, stream);

            Assert.That(viaPath, Is.EqualTo(stream.ToArray()));
        }

        /// <summary>
        /// Options reach the writer through the facade. <c>SourceName</c> is the one thing the SDK cannot
        /// derive — a project is a pure in-memory model with no path — so this is the caller's only way to
        /// name the file's subject.
        /// </summary>
        [Test]
        public async Task TheCallerSuppliedSourceNameReachesTheFile()
        {
            string text = await ExportToText(s => App().ExportFindings(
                Corpus(), s, FindingExportOptions.Default with { SourceName = "Project6-Errors.vis" }));

            Assert.That(text, Does.Contain(" source=\"Project6-Errors.vis\""));
        }

        /// <summary>Null options are the default options, not a failure.</summary>
        [Test]
        public async Task OmittedOptionsExportAsTheDefaults()
        {
            string text = await ExportToText(s => App().ExportFindings(Corpus(), s));

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain(" order=\"production\""));
                Assert.That(text, Does.Contain(" severities=\"Error Warning Info\""));
                Assert.That(text, Does.Contain(" source=\"\""));
            });
        }

        // ----- the sequence overloads -----

        /// <summary>
        /// The host's door: a caller-supplied sequence is emitted VERBATIM — same members, same order,
        /// nothing dropped and nothing added. Fed a reversed subset, the file is that reversed subset.
        /// </summary>
        [Test]
        public async Task TheSequenceOverloadEmitsTheCallerListVerbatim()
        {
            ProjectAppService app = App();
            Project project = Corpus();
            ImmutableArray<ValidationFinding> reversedSubset =
                [.. app.ValidateStructured(project).Findings.Take(5).Reverse()];

            string text = await ExportToText(s => app.ExportFindings(project, reversedSubset, s));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.FindingLines(text), Has.Length.EqualTo(5));
                Assert.That(
                    FindingExportProbe.FindingLines(text).Select(Code),
                    Is.EqualTo(reversedSubset.Select(f => f.Code.Value)));
            });
        }

        /// <summary>
        /// The sequence+PATH overload, which the host actually calls — it wants a file, and without this pair
        /// it would have to open and manage a stream for no reason.
        /// </summary>
        [Test]
        public async Task TheSequencePathOverloadWritesTheSameBytesAsItsStreamTwin()
        {
            ProjectAppService app = App();
            Project project = Corpus();
            ImmutableArray<ValidationFinding> subset = [.. app.ValidateStructured(project).Findings.Take(3)];
            var options = FindingExportOptions.Default with { SourceName = "panel", Order = "host:code" };
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "export-findings-sequence.xml");

            await app.ExportFindings(project, subset, path, options);
            byte[] viaPath = await File.ReadAllBytesAsync(path);

            using var stream = new MemoryStream();
            await app.ExportFindings(project, subset, stream, options);

            Assert.Multiple(() =>
            {
                Assert.That(viaPath, Is.EqualTo(stream.ToArray()));
                Assert.That(ProjectFile.Encoding.GetString(viaPath), Does.Contain(" order=\"host:code\""));
            });
        }

        /// <summary>
        /// The mechanism behind the flakiness the pair above used to carry, reproduced deterministically.
        ///
        /// <para>Every export stamps its own <c>generated</c> from the service's clock, to the SECOND. So two
        /// exports of one identical payload differ whenever the calls straddle a second boundary — which the
        /// byte comparison above hit about as often as a run takes to cross one. The clock is the ONLY thing
        /// that differs: with it advanced on purpose, the two documents are equal everywhere else.</para>
        ///
        /// <para>Which is why <see cref="App"/> holds a fixed clock rather than the system one. Nothing in this
        /// fixture asserts on what the timestamp SAYS — only that the same call twice writes the same bytes —
        /// so pinning it removes the race without weakening a single assertion.</para>
        /// </summary>
        [Test]
        public async Task TwoExportsDifferOnlyByTheirGeneratedStamp()
        {
            var clock = new FakeTimeProvider(FixedNow);
            var app = new ProjectAppService(TestSetup.Settings, Catalog, clock);
            Project project = Corpus();
            ImmutableArray<ValidationFinding> subset = [.. app.ValidateStructured(project).Findings.Take(3)];

            string first = await ExportToText(s => app.ExportFindings(project, subset, s));
            clock.Advance(TimeSpan.FromSeconds(1));
            string second = await ExportToText(s => app.ExportFindings(project, subset, s));

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.EqualTo(first), "a second's difference is enough to change the bytes");
                Assert.That(Without(second, "generated"), Is.EqualTo(Without(first, "generated")),
                    "and the stamp is the whole of the difference — nothing else in an export depends on when it ran");
            });
        }

        /// <summary>The document with one root attribute's value blanked, so two exports can be compared on
        /// everything else.</summary>
        private static string Without(string document, string attribute) =>
            System.Text.RegularExpressions.Regex.Replace(document, $" {attribute}=\"[^\"]*\"", $" {attribute}=\"\"");

        /// <summary>An empty caller sequence is a legal export, not an error: it is what an all-tiers-off panel has.</summary>
        [Test]
        public async Task AnEmptyCallerSequenceExportsAnEmptyDocument()
        {
            ProjectAppService app = App();
            string text = await ExportToText(s => app.ExportFindings(Corpus(), [], s));

            Assert.Multiple(() =>
            {
                Assert.That(FindingExportProbe.FindingLines(text), Is.Empty);
                Assert.That(text, Does.Contain("<ihc_project_findings "));
            });
        }

        // ----- no drift -----

        /// <summary>
        /// The default export is the SAME RUN the facade reports, not a second pipeline with its own rules:
        /// its <c>(code, severity, category, locator)</c> projection equals <c>ValidateStructured</c>'s, in
        /// order.
        ///
        /// <para><b>At that projection and no stronger, deliberately.</b> Asserting more — the message, the
        /// arguments, the paths — would restate the writer's own byte tests here and make this test fail for
        /// reasons that have nothing to do with drift. What it exists to catch is an export that quietly ran a
        /// DIFFERENT profile: a third profile invented for export would give the file a different rule set
        /// from the screen, which is exactly what the two-faces-one-rule-set invariant forbids.</para>
        ///
        /// <para>And it is NOT true of the host path by design — a host exports what its panel shows, filtered
        /// and re-sorted. That fidelity is the host's own test to make.</para>
        /// </summary>
        [Test]
        public async Task TheDefaultExportIsTheSameRunTheFacadeReports()
        {
            ProjectAppService app = App();
            Project project = Corpus();

            string text = await ExportToText(s => app.ExportFindings(project, s));
            var exported = FindingExportProbe.FindingLines(text)
                .Select(l => (
                    Code: FindingExportProbe.Value(l, "code"),
                    Severity: FindingExportProbe.Value(l, "severity"),
                    Category: FindingExportProbe.Value(l, "category"),
                    Locator: l.Contains(" locator=\"") ? FindingExportProbe.Value(l, "locator") : null))
                .ToImmutableArray();

            var reported = app.ValidateStructured(project).Findings
                .Select(f => (
                    Code: f.Code.Value,
                    Severity: f.Severity.ToString(),
                    Category: f.Category.ToString(),
                    Locator: f.Primary?.Locator))
                .ToImmutableArray();

            Assert.Multiple(() =>
            {
                Assert.That(exported, Is.Not.Empty, "non-vacuity: this fixture really does produce findings");
                Assert.That(exported, Is.EqualTo(reported));
            });
        }

    }
}
