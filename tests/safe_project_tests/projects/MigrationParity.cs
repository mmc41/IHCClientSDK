#nullable enable
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

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

        /// <summary>One corpus case by name, built through the same fixtures the recording was made from.</summary>
        internal static Project CorpusCase(string name) =>
            ValidationCharacterizationTests.Corpus.Single(c => c.Case == name).Build();

        /// <summary>The recorded rows for the given rule ids, grouped by corpus case.</summary>
        internal static ILookup<string, string[]> Recorded(ImmutableArray<string> ruleIds)
        {
            string path = TestData.PathOf("validation", "rule-characterization.txt");
            Assert.That(File.Exists(path), Is.True, $"the characterization recording is missing at {path}");
            return File.ReadAllLines(path, Encoding.UTF8)
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Select(line => line.Split('\t'))
                .Where(cells => ruleIds.Contains(cells[2]))
                .ToLookup(cells => cells[0], cells => cells);
        }

        /// <summary>
        /// Asserts the given rules reproduce the recording for their own ids, per corpus case. Fails the whole
        /// check when the corpus witnesses none of them, since a parity gate over nothing proves nothing.
        /// </summary>
        /// <param name="ruleIds">The ids these rules own.</param>
        /// <param name="rules">The migrated rules, already registered.</param>
        internal static void AssertReproducesRecording(ImmutableArray<string> ruleIds, RuleSet rules)
        {
            WholeProjectValidator engine = new(rules);
            ILookup<string, string[]> recorded = Recorded(ruleIds);

            Assert.That(recorded, Is.Not.Empty, "the corpus must witness these rules, or this gate is vacuous");

            Assert.Multiple(() =>
            {
                foreach (IGrouping<string, string[]> forCase in recorded)
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

        private static string Row(ValidationFinding finding) =>
            string.Join('\t', finding.Severity, finding.Code.Value, finding.Category,
                finding.Primary?.Locator ?? "-", finding.Problem.Message);

        private static string Expected(string[] recorded)
        {
            Catalog.TryGet(new ProblemCode(recorded[2]), out ProblemCatalogEntry entry);
            return string.Join('\t', recorded[1], recorded[2], entry.Category, recorded[4], entry.MessageTemplate);
        }
    }
}
