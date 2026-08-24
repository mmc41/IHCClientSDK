using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE ERROR-ORIGIN INVENTORY, and the fitness test that keeps it honest: every place the SDK's
    /// <c>.vis</c> surface tells someone something went wrong, classified as a refusal, a failure or a finding.
    ///
    /// <para><b>Why an inventory at all.</b> "Every user-facing error has a code" is a claim nobody can check by
    /// reading, because the origins are scattered across a session layer, a serializer, a schema guard and an
    /// application service, in four different shapes. The inventory turns that claim into a list, and the scan
    /// below turns the list into a gate: an origin the source has and the inventory does not fails here, so an
    /// uncoded refusal cannot be added quietly.</para>
    ///
    /// <para><b>The classification is the substance, not the count.</b> A REFUSAL tells a user, in Danish, that
    /// an operation will not be carried out, and needs a code. A FAILURE is something broken: its English
    /// diagnostic goes to the log and the user gets a fixed Danish label, so it needs no code beyond the
    /// catch-all. A FINDING is a report about a file's content rather than about an operation, and is coded
    /// already. Mixing the first two is the invariant-10 breach this whole mechanism exists to end.</para>
    ///
    /// <para><b>What is excluded, and why that is a decision rather than an omission.</b> Argument guards — about
    /// a hundred of them — are left out: a public method called with an out-of-contract argument is a
    /// programming error in the CALLER, not an outcome a user can act on, and giving one a Danish sentence would
    /// put a bug report in front of an installer. The controller API tier is out of scope for this step; it is a
    /// separate surface with its own outcomes.</para>
    /// </summary>
    [TestFixture]
    public sealed class ErrorOriginInventoryTests
    {
        private const string InventoryFile = "validation/error-origins.txt";

        /// <summary>The origin shapes, and the pattern that finds each in source.</summary>
        private static readonly ImmutableArray<(string Shape, string Pattern)> Shapes =
        [
            ("edit-verdict-refusal", @"EditVerdict\.Refuse\("),
            ("deep-guard-refusal", @"throw new EditRefusedException\("),
            ("preview-refusal", @"PreviewOutcome\.Refused\("),
            ("operation-throw", @"throw new (?:InvalidOperationException|IOException|FormatException|NotSupportedException)\("),
            // The MIGRATED shape. A refusal that has been given a code stops matching operation-throw, so
            // without this row the inventory would quietly shrink as the work lands and stop being able to say
            // how much of it is done. Counting both shapes is what makes the artifact a progress ledger rather
            // than a snapshot of what is left.
            ("coded-refusal", @"throw new (?:RefusedOperationException|RefusedWriteException|RefusedImportException|ProjectFormatException|ProjectUploadException)\("),
            ("validation-finding", @"new ProjectValidationFinding\("),
        ];

        private static readonly ImmutableArray<string> Scope =
            ["ihcclient/src/vis/", "ihcclient/src/app/services/ProjectAppService.cs"];

        private sealed record Origin(string File, string Shape, int Count, string Family, string Classification);

        [Test]
        public void EveryOriginInTheSourceIsInTheInventory()
        {
            ImmutableArray<Origin> scanned = Scan();
            ImmutableArray<Origin> recorded = Recorded();

            string[] missing = [.. scanned
                .Where(o => !recorded.Any(r => r.File == o.File && r.Shape == o.Shape && r.Count == o.Count))
                .Select(o => $"{o.File}\t{o.Shape}\t{o.Count}")];
            string[] stale = [.. recorded
                .Where(r => !scanned.Any(o => o.File == r.File && o.Shape == r.Shape && o.Count == r.Count))
                .Select(r => $"{r.File}\t{r.Shape}\t{r.Count}")];

            Assert.Multiple(() =>
            {
                Assert.That(scanned, Is.Not.Empty, "the scan must find origins, or this gate is vacuous");
                Assert.That(missing, Is.Empty,
                    "these error origins are in the source and not in the inventory — classify them as a refusal "
                    + "(needs a code) or a failure (English to the log), then add the rows:"
                    + Environment.NewLine + string.Join(Environment.NewLine, missing));
                Assert.That(stale, Is.Empty,
                    "these inventory rows no longer match the source:"
                    + Environment.NewLine + string.Join(Environment.NewLine, stale));
            });
        }

        /// <summary>
        /// The classification vocabulary is closed, and every row uses one of its three values. A fourth would
        /// mean a fourth thing an origin can be, which is a design change rather than a row edit.
        /// </summary>
        [Test]
        public void EveryOriginIsClassifiedAndCarriesAFamily()
        {
            ImmutableArray<Origin> recorded = Recorded();

            Assert.Multiple(() =>
            {
                Assert.That(recorded.Select(o => o.Classification).Distinct().OrderBy(c => c, StringComparer.Ordinal),
                    Is.EqualTo(new[] { "Failure", "Finding", "Refusal" }).AsCollection);

                foreach (Origin origin in recorded)
                {
                    Assert.That(origin.Family, Is.Not.Empty, origin.File);
                    ProblemCode code = new(origin.Family == "validation" ? "some-row" : origin.Family + ".some-outcome");
                    Assert.That(code.Family, Is.Not.EqualTo(ProblemFamily.Unknown),
                        $"{origin.File}: '{origin.Family}' is not a family the code scheme knows");
                }
            });
        }

        /// <summary>
        /// The already-coded population is in here too, so the inventory and the catalogue describe ONE set of
        /// origins rather than two overlapping ones. The ten catalog-definition codes are existing user-facing
        /// output, and their origins are the three definition builders.
        /// </summary>
        [Test]
        public void TheAlreadyCodedDefinitionFindingsAppearWithTheirFamily()
        {
            ImmutableArray<Origin> findings = [.. Recorded().Where(o => o.Classification == "Finding")];

            Assert.Multiple(() =>
            {
                Assert.That(findings.Select(o => o.File), Does.Contain("ihcclient/src/vis/products/ProductDefinitionBuilder.cs"));
                Assert.That(findings.Select(o => o.File), Does.Contain("ihcclient/src/vis/functionblocks/FunctionBlockDefinitionBuilder.cs"));
                Assert.That(findings, Is.All.Matches<Origin>(o => o.Family == "validation"));

                // And the codes those origins emit are governed, which is what makes the two artifacts agree.
                foreach (string code in new[] { "identity-missing", "scenes-without-output", "resource-enum-unwired", "program-empty" })
                {
                    Assert.That(ProblemCatalog.Current.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.Section, Is.EqualTo(ProblemCatalogSection.CatalogDefinitionFindings), code);
                }
            });
        }

        /// <summary>
        /// Every REFUSAL origin is a place a Danish sentence reaches a user, so the inventory's refusal count is
        /// the size of the work the coded-refusal tasks have to cover. Pinned so the number cannot drift
        /// unnoticed between here and there.
        /// </summary>
        [Test]
        public void TheRefusalPopulationIsTheWorkTheCodedRefusalTasksMustCover()
        {
            ImmutableArray<Origin> recorded = Recorded();
            int refusals = recorded.Where(o => o.Classification == "Refusal").Sum(o => o.Count);
            int editRefusals = recorded.Where(o => o is { Classification: "Refusal", Family: "edit" }).Sum(o => o.Count);

            Assert.Multiple(() =>
            {
                Assert.That(editRefusals, Is.EqualTo(34),
                    "the session's own refusals: 25 verdict sites, 6 deep guards and 3 preview refusals");
                Assert.That(refusals, Is.GreaterThan(editRefusals),
                    "plus the load, save and schema-guard refusals, which are the io family's");
            });
        }

        private static ImmutableArray<Origin> Recorded()
        {
            string path = TestData.PathOf(InventoryFile);
            Assert.That(File.Exists(path), Is.True, $"the checked-in inventory is missing at {path}");
            return
            [
                .. File.ReadAllLines(path, Encoding.UTF8)
                    .Where(line => line.Length > 0 && !line.StartsWith('#'))
                    .Select(line => line.Split('\t'))
                    .Select(cells => new Origin(cells[0], cells[1], int.Parse(cells[2]), cells[3], cells[4])),
            ];
        }

        private static ImmutableArray<Origin> Scan()
        {
            string root = TestRepository.RequireRoot();
            var found = ImmutableArray.CreateBuilder<Origin>();
            foreach (string absolute in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, absolute).Replace('\\', '/');
                if (!Scope.Any(s => relative.StartsWith(s, StringComparison.Ordinal)) || relative.Contains("generatedsrc"))
                {
                    continue;
                }

                string source = File.ReadAllText(absolute, Encoding.UTF8);
                foreach ((string shape, string pattern) in Shapes)
                {
                    int count = Regex.Matches(source, pattern).Count;
                    if (count > 0)
                    {
                        found.Add(new Origin(relative, shape, count, string.Empty, string.Empty));
                    }
                }
            }

            return found.ToImmutable();
        }
    }
}
