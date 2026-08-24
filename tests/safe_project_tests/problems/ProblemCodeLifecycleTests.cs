using System;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The code lifecycle, which exists before the first family ships because retrofitting one is impossible
    /// once a catalogue has consumers.
    ///
    /// <para>It is deliberately small — one enum and one degradation property — and the smallness is the design.
    /// An earlier shape had a lifecycle class and a retired-code record with a "follow this retirement to its
    /// replacement" member that could not be implemented, because nothing held the replacements. What survives
    /// is what carries the two obligations:</para>
    /// <list type="bullet">
    /// <item><description><b>Retirement reserves the id.</b> Expressed by the ABSENCE of a "removed" status: a
    /// code that stops being minted keeps its entry, so the catalogue's duplicate-code invariant already refuses
    /// to reuse it. No separate reserved-id list is needed, and none exists to fall out of
    /// sync.</description></item>
    /// <item><description><b>An unknown code degrades.</b> A consumer meeting a code from a later SDK reads the
    /// message, groups it under <see cref="ProblemFamily.Unknown"/>, and carries on. The parser-side half of this
    /// is pinned in <see cref="ProblemContractTests"/>; what is pinned here is the CONSUMER contract — that
    /// everything a renderer needs is still readable.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class ProblemCodeLifecycleTests
    {
        [Test]
        public void TheStatusVocabularyIsExactlyThreeMembers()
        {
            string[] names = Enum.GetNames<ProblemCodeStatus>();

            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EquivalentTo(new[]
                {
                    nameof(ProblemCodeStatus.Active),
                    nameof(ProblemCodeStatus.Retired),
                    nameof(ProblemCodeStatus.RuledOut),
                }));

                // Retired and RuledOut are different facts, not synonyms: one code WAS minted and is not any
                // more, the other was examined and never will be. Both reserve the id.
                Assert.That(ProblemCodeStatus.Retired, Is.Not.EqualTo(ProblemCodeStatus.RuledOut));
            });
        }

        /// <summary>
        /// Deprecation is the courtesy of keeping something working while asking callers to move off it. This SDK
        /// declines that one level up — no obsolete shims, no aliases, no compatibility overloads — so offering
        /// it for a code would be inconsistent, and a status nobody may act on is a status that rots.
        /// </summary>
        [Test]
        public void TheVocabularyCannotExpressDeprecation()
        {
            string[] names = Enum.GetNames<ProblemCodeStatus>();

            Assert.Multiple(() =>
            {
                foreach (string shim in new[] { "Deprecated", "Obsolete", "Legacy", "Superseded" })
                {
                    Assert.That(names, Does.Not.Contain(shim),
                        $"'{shim}' would promise a compatibility posture this SDK does not offer");
                }
            });
        }

        /// <summary>
        /// The reservation mechanism, stated as the property it actually rests on. Nothing here can say a code is
        /// GONE, so a retired code still has an entry, the entry still occupies the id, and reuse is refused by
        /// the ordinary duplicate-code check rather than by a second mechanism that could disagree with it.
        /// </summary>
        [Test]
        public void NoStatusMeansTheCodeIsGone_WhichIsWhatMakesRetirementAReservation()
        {
            string[] names = Enum.GetNames<ProblemCodeStatus>();

            Assert.Multiple(() =>
            {
                foreach (string gone in new[] { "Removed", "Deleted", "Gone", "Unpublished", "Free" })
                {
                    Assert.That(names, Does.Not.Contain(gone),
                        $"'{gone}' would release the id and let a later condition silently inherit it");
                }
            });
        }

        /// <summary>
        /// The consumer half of the unknown-code rule: a host built against this SDK meets a code a later version
        /// introduced, and every part a renderer needs still works.
        /// </summary>
        [Test]
        public void AConsumerMeetingAnUnknownCodeCanStillRenderTheProblem()
        {
            Problem fromTheFuture = new(
                new ProblemCode("telemetry.export-refused"),
                "Eksport afvist",
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("count", 3)]),
                Diagnostic: "The telemetry exporter refused the batch.");

            Assert.Multiple(() =>
            {
                Assert.That(fromTheFuture.Code.Family, Is.EqualTo(ProblemFamily.Unknown), "grouped, not rejected");
                Assert.That(fromTheFuture.Message, Is.EqualTo("Eksport afvist"), "the message is still shown");
                Assert.That(fromTheFuture.Code.Value, Is.EqualTo("telemetry.export-refused"), "identity survives");
                Assert.That(fromTheFuture.Code.ExplanationAnchor, Is.EqualTo("telemetry-export-refused"));
                Assert.That(fromTheFuture.Arguments.Single().Value, Is.EqualTo(3));
                Assert.That(fromTheFuture.Code.IsHostOwned, Is.False, "an unknown family is not the host family");
            });
        }
    }
}
