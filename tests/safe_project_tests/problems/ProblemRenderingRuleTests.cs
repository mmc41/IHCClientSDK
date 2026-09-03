using System;
using System.Collections.Generic;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The parent/child RENDERING RULE, so composition never degrades into fragment assembly.
    ///
    /// <para><b>Why this is a test and not a shipped renderer.</b> The SDK deliberately has no renderer type: an
    /// SDK-side one would duplicate the shell's single presentation path, and its flattened output could not
    /// carry the per-item identity that path needs. So the rule is STATED on the three types that express it —
    /// <see cref="Problem"/>, <see cref="ProblemChain"/>, <see cref="ProblemAggregate"/>, each carrying its own
    /// case in a <c>&lt;remarks&gt;</c> block — and pinned here by a reference implementation that lives in the
    /// test. What the shell builds later must produce the same three shapes.</para>
    ///
    /// <para><b>The rule, in one place:</b></para>
    /// <list type="number">
    /// <item><description><b>Bare problem</b> → its message, whole. Identity subordinate: a bracketed suffix
    /// AFTER the message, never a prefix that displaces it.</description></item>
    /// <item><description><b>Chain</b> → the CAUSE's message, once. The operation's sentence is never shown
    /// beside it; its code stays available for grouping.</description></item>
    /// <item><description><b>Aggregate</b> → the head, then EVERY item as its own complete entry, in
    /// order.</description></item>
    /// </list>
    ///
    /// <para>Applying either composition rule to the other shape is the defect: a chain rendered as a list shows
    /// one failure twice, and an aggregate reduced to its most specific member loses N−1 findings.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProblemRenderingRuleTests
    {
        // The reference implementation of the stated rule. Three overloads, one per case, each a property read.
        private static string Render(Problem problem) => $"{problem.Message} [{problem.Code.Value}]";

        private static string Render(ProblemChain chain) => Render(chain.Cause);

        private static IReadOnlyList<string> Render(ProblemAggregate aggregate) =>
            [Render(aggregate.Head), .. aggregate.Items.Select(Render)];

        private static Problem P(string code, string message) =>
            new(new ProblemCode(code), message, EquatableArray<ProblemArgument>.Empty);

        [Test]
        public void Case1_ABareProblemRendersWholeWithIdentitySubordinate()
        {
            Problem problem = P("doc-cabletype", "Mangler Kabeltype");
            string rendered = Render(problem);

            Assert.Multiple(() =>
            {
                Assert.That(rendered, Does.StartWith("Mangler Kabeltype"),
                    "the message leads; identity never displaces it");
                Assert.That(rendered, Does.EndWith("[doc-cabletype]"), "identity is a suffix, and bracketed");
                Assert.That(rendered, Does.Not.StartWith("doc-cabletype"));
                Assert.That(rendered, Does.Contain(problem.Message), "the message is carried whole, not in parts");
            });
        }

        [Test]
        public void Case1_TheEnglishDiagnosticNeverReachesTheRenderedForm()
        {
            Problem problem = new(
                new ProblemCode("io.load"),
                "Projektet kunne ikke åbnes",
                EquatableArray<ProblemArgument>.Empty,
                Diagnostic: "The stream ended before the root element was closed.");

            Assert.That(Render(problem), Does.Not.Contain(problem.Diagnostic!),
                "the engine sentence goes to the log, never beside the Danish message");
        }

        [Test]
        public void Case2_AChainRendersTheCauseOnceAndNeverTheOperationsSentence()
        {
            ProblemChain chain = new(P("io.load", "Projektet kunne ikke åbnes"), P("load-empty", "Filen er tom"));
            string rendered = Render(chain);

            Assert.Multiple(() =>
            {
                Assert.That(rendered, Does.StartWith("Filen er tom"), "the more specific of the two levels");
                Assert.That(rendered, Does.Not.Contain(chain.Operation.Message),
                    "showing both levels shows the user one failure twice");
                Assert.That(CountOccurrences(rendered, "Filen er tom"), Is.EqualTo(1));

                // The operation is still identifiable, without its sentence being rendered.
                Assert.That(chain.Operation.Code.Value, Is.EqualTo("io.load"));
            });
        }

        [Test]
        public void Case3_AnAggregateRendersEveryItemAndTheHead()
        {
            Problem[] items =
            [
                P("doc-cabletype", "Mangler Kabeltype"),
                P("doc-position", "Mangler Placering"),
                P("doc-not-linked", "Ikke forbundet"),
            ];
            ProblemAggregate aggregate = new(P("io.save", "Projektet har fejl"), EquatableArray.Create<Problem>(items));

            IReadOnlyList<string> rendered = Render(aggregate);

            Assert.Multiple(() =>
            {
                Assert.That(rendered, Has.Count.EqualTo(items.Length + 1), "the head plus every item, nothing elided");
                Assert.That(rendered[0], Does.StartWith("Projektet har fejl"));
                foreach (Problem item in items)
                {
                    Assert.That(rendered, Has.Some.StartsWith(item.Message), item.Code.Value);
                }
            });
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }
    }
}
