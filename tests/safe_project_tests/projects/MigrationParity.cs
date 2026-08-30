#nullable enable
using System.Collections.Immutable;
using System.Linq;

using Ihc.Tests.Shared;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// What the migration fixtures share: a corpus case by name, the recording narrowed to a set of codes, and
    /// the catalogue declaration check every migrated rule owes.
    ///
    /// <para><b>Comparing engine output against the recording is deliberately NOT here.</b> Each fixture used to
    /// re-check its own rules' findings against the recording, per case. That comparison is subsumed by
    /// <c>ValidationCharacterizationTests.Corpus_ReproducesItsOracleByteForByte</c>, which runs the whole
    /// <c>ProjectAppService</c> over the same corpus and compares byte for byte — so it also pins the argument
    /// values, node paths, run caveats and production ORDER that a per-rule subset comparison could not see. A
    /// weaker second copy of it here would only be somewhere for the two to disagree.</para>
    /// </summary>
    internal static class MigrationParity
    {
        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        /// <summary>One corpus case by name, built through the same fixtures the recording was made from.</summary>
        internal static Project CorpusCase(string name) =>
            ValidationCharacterizationTests.Corpus.Single(c => c.Case == name).Build();

        /// <summary>
        /// The recorded findings for the given codes, grouped by corpus case.
        /// <para>
        /// Through the shared oracle reader, so the cells arrive already named. This used to split each line on
        /// tabs and index into the result, which was a fourth copy of the recording's column layout and an
        /// unchecked assumption that column 2 held the code.
        /// </para>
        /// </summary>
        internal static ILookup<string, RecordedFinding> Recorded(ImmutableArray<string> ruleIds) =>
            FindingOracleHarness.ReadAll()
                .Where(finding => ruleIds.Contains(finding.Code))
                .ToLookup(finding => finding.Case);

        /// <summary>Every migrated entry carries the two halves the language split requires, and nothing else moved.</summary>
        /// <param name="ruleIds">The ids to check.</param>
        /// <param name="kind">The rule kind every one of them declares.</param>
        internal static void AssertDeclaredWithBothLanguages(ImmutableArray<string> ruleIds, RuleKind kind)
        {
            Assert.Multiple(() =>
            {
                foreach (string code in ruleIds)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, $"{code}: the user-facing Danish label");
                    Assert.That(entry.Diagnostic, Is.Not.Null.And.Not.Empty, $"{code}: the English engine sentence");
                    Assert.That(entry.Kind, Is.EqualTo(kind), code);
                }
            });
        }

    }
}
