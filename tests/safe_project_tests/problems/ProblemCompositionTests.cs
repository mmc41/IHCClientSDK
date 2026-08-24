using System;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The two child relationships a problem can have, and the reason they are two TYPES rather than one shape
    /// with a flag.
    ///
    /// <para>A <see cref="ProblemChain"/> is a CAUSE/DETAIL pair: an operation outcome and the one condition that
    /// caused it, both describing the same failure at different precision — <i>Projektet kunne ikke åbnes</i> ←
    /// <i>Filen er tom</i>. A <see cref="ProblemAggregate"/> is N INDEPENDENT problems, each about a different
    /// thing, all of which must be shown.</para>
    ///
    /// <para><b>The defect these tests exist to make unwritable.</b> Conflating the two is a real failure in both
    /// directions: applying "use the most detailed child" to an aggregate silently discards N−1 findings, and
    /// rendering a chain as a list shows the user one failure twice. Separate types make the first of those
    /// not compile — an aggregate has no most-specific member to reach for — and the structural test below is
    /// what keeps that true as members are added later.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProblemCompositionTests
    {
        private static Problem Op(string code, string message, params ProblemArgument[] arguments) =>
            new(new ProblemCode(code), message, EquatableArray.Create<ProblemArgument>(arguments));

        [Test]
        public void Chain_KeepsTheOperationIdentifiableAndTheCausePublished()
        {
            // The operation carries the dotted family code; the cause keeps the bare published catalogue id.
            ProblemChain chain = new(
                Op("io.load", "Projektet kunne ikke åbnes"),
                Op("load-empty", "Filen er tom", new ProblemArgument("path", @"C:\projekter\tom.vis")));

            Assert.Multiple(() =>
            {
                Assert.That(chain.Operation.Code.Family, Is.EqualTo(ProblemFamily.Io));
                Assert.That(chain.Cause.Code.Family, Is.EqualTo(ProblemFamily.Validation),
                    "the cause keeps its bare catalogue id — no dotted 'io.load-empty' is minted");
                Assert.That(chain.Cause.Code.Value, Is.EqualTo("load-empty"));

                // The traversal a renderer performs: one step, to the more specific of exactly two levels.
                Assert.That(chain.Cause.Message, Is.EqualTo("Filen er tom"));
                Assert.That(chain.Operation.Message, Is.EqualTo("Projektet kunne ikke åbnes"));
            });
        }

        [Test]
        public void Chain_IsTwoLevelsAndCannotNest()
        {
            Assert.Multiple(() =>
            {
                // "At most one child per level" is structural, not a comment: the cause is a Problem, and a
                // Problem has no child of its own, so a third level cannot be expressed.
                Assert.That(typeof(Problem).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => p.PropertyType),
                    Has.None.EqualTo(typeof(ProblemChain)).Or.EqualTo(typeof(ProblemAggregate)),
                    "composition is never a nullable child field on Problem");

                Assert.That(typeof(ProblemChain).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => p.Name),
                    Is.EquivalentTo(new[] { nameof(ProblemChain.Operation), nameof(ProblemChain.Cause) }));
            });
        }

        [Test]
        public void Aggregate_RendersEveryItemAndNeverCollapsesOneIntoTheHead()
        {
            Problem[] items =
            [
                Op("doc-cabletype", "Mangler Kabeltype"),
                Op("doc-position", "Mangler Placering"),
                Op("doc-not-linked", "Ikke forbundet"),
            ];

            ProblemAggregate aggregate = new(
                Op("io.save", "Projektet kunne ikke gemmes", new ProblemArgument("count", items.Length)),
                EquatableArray.Create<Problem>(items));

            Assert.Multiple(() =>
            {
                Assert.That(aggregate.Items, Is.EqualTo(items).AsCollection,
                    "every item is rendered, in the producer's stable order");
                Assert.That(aggregate.Head.Arguments.Single().Value, Is.EqualTo(3),
                    "the head names the failure as a whole, with the count as a declared datum");
            });
        }

        /// <summary>
        /// The R12(e) guard, stated as a property of the type rather than as a convention. An aggregate exposes
        /// no member that reduces it to one problem, so "use the most detailed child" cannot be written against
        /// it — which is what stops N−1 independent findings being discarded by a renderer that meant well.
        /// </summary>
        [Test]
        public void Aggregate_ExposesNoWayToReduceItselfToASingleProblem()
        {
            string[] members = typeof(ProblemAggregate)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(typeof(ProblemAggregate).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => p.Name),
                    Is.EquivalentTo(new[] { nameof(ProblemAggregate.Head), nameof(ProblemAggregate.Items) }));

                foreach (string reducer in new[] { "MostSpecific", "Innermost", "Single", "Flatten", "Cause" })
                {
                    Assert.That(members, Does.Not.Contain(reducer),
                        $"a '{reducer}' member would let a renderer discard every item but one");
                }
            });
        }

        [Test]
        public void Aggregate_WithAppendsInOrderAndLeavesTheOriginalUntouched()
        {
            Problem first = Op("doc-cabletype", "Mangler Kabeltype");
            Problem second = Op("doc-position", "Mangler Placering");

            ProblemAggregate one = new(Op("io.save", "Projektet kunne ikke gemmes"), EquatableArray.Create<Problem>([first]));
            ProblemAggregate two = one.With(second);

            Assert.Multiple(() =>
            {
                Assert.That(two.Items, Is.EqualTo(new[] { first, second }).AsCollection);
                Assert.That(one.Items, Is.EqualTo(new[] { first }).AsCollection, "the original aggregate is unchanged");
                Assert.That(two.Head, Is.EqualTo(one.Head));
            });
        }

        [Test]
        public void BothShapes_CompareByValue()
        {
            Problem operation = Op("io.load", "Projektet kunne ikke åbnes");
            Problem cause = Op("load-empty", "Filen er tom");

            ProblemChain chain = new(operation, cause);
            ProblemChain sameChain = new(operation, cause);
            ProblemAggregate aggregate = new(operation, EquatableArray.Create<Problem>([cause]));
            ProblemAggregate sameAggregate = new(operation, EquatableArray.Create<Problem>([cause]));

            Assert.Multiple(() =>
            {
                Assert.That(chain, Is.EqualTo(sameChain));
                Assert.That(aggregate, Is.EqualTo(sameAggregate));
                // A chain and an aggregate over the same two problems are never equal: they are different types
                // saying different things.
                Assert.That((object)chain, Is.Not.EqualTo(aggregate));
            });
        }
    }
}
