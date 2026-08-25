#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Tests.Shared;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The shared parity machinery every Phase-5 migration fixture uses: build the migrated rules on the engine,
    /// run them over the corpus, and compare what they say against the recording made before any of them moved.
    ///
    /// <para><b>What is compared, and why it is a per-case CONTENT comparison rather than a positional one.</b>
    /// Severity, rule id and locator must reproduce the recording exactly. The message and the category are
    /// asserted to their new declared values — the Danish label and the catalogue's real category — because both
    /// change on purpose for every migrated rule. What is deliberately NOT compared here is the position of a
    /// finding in the whole run: the shipped pipeline emits in pass order and the engine in document order, so
    /// until orchestration moves there is no single sequence both could satisfy. The whole-run order stays gated
    /// by the shipped pipeline's own recording, which keeps passing untouched, and by the executor's determinism
    /// test.</para>
    /// </summary>
    internal static class MigrationParity
    {
        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        /// <summary>How a comparison row shows a finding that names no element.</summary>
        private const string NoLocator = "<none>";

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

        /// <summary>
        /// Asserts the given rules reproduce the recording for their own ids, per corpus case. Fails the whole
        /// check when the corpus witnesses none of them, since a parity gate over nothing proves nothing.
        /// </summary>
        /// <param name="ruleIds">The ids these rules own.</param>
        /// <param name="rules">The migrated rules, already registered.</param>
        internal static void AssertReproducesRecording(ImmutableArray<string> ruleIds, RuleSet rules)
        {
            WholeProjectValidator engine = new(rules);
            ILookup<string, RecordedFinding> recorded = Recorded(ruleIds);

            Assert.That(recorded, Is.Not.Empty, "the corpus must witness these rules, or this gate is vacuous");

            Assert.Multiple(() =>
            {
                foreach (IGrouping<string, RecordedFinding> forCase in recorded)
                {
                    string[] produced = [.. engine.Validate(CorpusCase(forCase.Key), ValidationProfile.Categorized)
                        .Select(Row)
                        .OrderBy(row => row, StringComparer.Ordinal)];
                    string[] expected = [.. forCase.Select(Expected).OrderBy(row => row, StringComparer.Ordinal)];

                    Assert.That(produced, Is.EqualTo(expected).AsCollection, forCase.Key);
                }
            });
        }

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

        /// <summary>
        /// A produced finding as a comparison row. Internal so the schema-rule fixture builds its rows the same
        /// way: two fixtures formatting one tuple by hand is how the two sides come to disagree about a cell.
        /// </summary>
        internal static string Row(ValidationFinding finding) =>
            Format(
                finding.Severity.ToString(), finding.Code.Value, finding.Category.ToString(),
                finding.Primary?.Locator, finding.Problem.Message);

        /// <summary>
        /// The recorded finding as the engine must reproduce it. The CATEGORY comes from the entry because a
        /// migration moves it on purpose; the MESSAGE comes from the recording.
        /// <para>
        /// The message used to be read from <see cref="ProblemCatalogEntry.MessageTemplate"/> too, which was only
        /// ever right for a row whose sentence has no argument slots — the template and the message are then the
        /// same string. A row that surfaces its arguments produces a BOUND message, so a template-based
        /// expectation would demand the engine emit a literal <c>{tag}</c>. The recording already holds the bound
        /// sentence, and it is regenerated deliberately and diffed, so comparing against it is the stronger check:
        /// the fixture's rule set and the production rule set must say the same words.
        /// </para>
        /// </summary>
        internal static string Expected(RecordedFinding recorded)
        {
            Catalog.TryGet(new ProblemCode(recorded.Code), out ProblemCatalogEntry entry);
            // An entry's category is nullable; the recording rendered a null one as an empty cell, and that is
            // preserved rather than corrected here — a migration gate is not the place to change what a cell
            // means.
            return Format(
                recorded.Severity, recorded.Code, entry.Category?.ToString() ?? string.Empty,
                recorded.Locator, recorded.Message);
        }

        /// <summary>
        /// The ONE place a comparison row is spelled, so both sides render an absent locator identically.
        /// <para>
        /// That is what the move to XML made load-bearing. The tab-separated recording carried a <c>-</c>
        /// sentinel for a finding that names no element — a sentinel no row in the corpus ever actually used —
        /// while the new format records absence as a MISSING attribute, which reads back as <c>null</c>. Two
        /// independently written formatters would have disagreed the first time a whole-project finding appeared
        /// in a migrated rule's set, comparing "-" against "" while both sides were right about their own half.
        /// </para>
        /// </summary>
        private static string Format(
            string severity, string code, string category, string? locator, string message) =>
            string.Join('\t', severity, code, category, locator ?? NoLocator, message);
    }
}
