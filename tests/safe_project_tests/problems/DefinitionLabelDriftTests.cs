using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Problems;
using Ihc.Vis.Products;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The drift gate for the DEFINITION-file raisers, the counterpart of
    /// <see cref="RefusalLabelDriftTests"/> one layer over: every Danish sentence a `.def`/`.ifb` builder or the
    /// grammar advisor carries is the same words as its catalogue entry's template.
    ///
    /// <para><b>Why the words are written twice at all.</b> These raisers sit below the validation engine and may
    /// not read the catalogue, so the sentence a user sees has to be a literal beside the code — the same shape
    /// <see cref="Ihc.Vis.Problems.RefusalIdentity"/> uses for a refusing site. What can be removed is the risk,
    /// not the duplication: provoke every code, read what it actually says, and require agreement.</para>
    ///
    /// <para><b>Provoked, not scanned.</b> Each code is raised by driving a builder into the state that raises it,
    /// so the gate reads the sentence a user would actually get — see <see cref="DefinitionFindingProbe"/>, which
    /// the severity gate in <see cref="CatalogCompletenessTests"/> reads too. A source-text scan would pass on a
    /// raiser that spells the words correctly in a branch nothing reaches, and would miss one that builds its
    /// message any way other than the pattern the regular expression expects.</para>
    ///
    /// <para>That the probe REACHES every declared definition code — without which this gate would silently
    /// shrink to whatever the provocations still happen to raise — is asserted once for both gates, in
    /// <see cref="CatalogCompletenessTests.TheProbeReachesEveryCatalogDefinitionCode"/>.</para>
    ///
    /// <para><b>The English is not lost, it is RELOCATED.</b> Each of these messages used to be the English
    /// sentence, and the English generally says more than a Danish label can — which attribute, which tag, which
    /// enumeration. It moves to <see cref="ProjectValidationFinding.Diagnostic"/>, and that it survived is
    /// asserted here rather than assumed.</para>
    /// </summary>
    [TestFixture]
    public sealed class DefinitionLabelDriftTests
    {
        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        /// <summary>
        /// The gate: every provoked finding says what its entry says. UNCONDITIONALLY — there is no exemption
        /// list and no branch, which is the point of the shape.
        /// <para>
        /// One raiser used to be excused. <c>identity-missing</c> was raised by the product builder and the
        /// function-block builder about two different conditions, under one entry reading <i>Mangler
        /// produktidentitet</i> — true of the product, false of the block — so the block site kept an English
        /// sentence rather than tell a user something untrue in their own language. The repair was always to
        /// SPLIT the code, and <c>block-identity-missing</c> is that split; the exemption went with it. A
        /// mismatch is now simply a failure, with nowhere to write it down.
        /// </para>
        /// </summary>
        [Test]
        public void EveryDefinitionFindingSaysWhatItsEntrySays()
        {
            ImmutableArray<(string Raiser, ProjectValidationFinding Finding)> provoked = DefinitionFindingProbe.Provoked();

            Assert.Multiple(() =>
            {
                Assert.That(provoked, Is.Not.Empty, "the raisers are the evidence; provoking none proves nothing");

                foreach ((string raiser, ProjectValidationFinding finding) in provoked)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(finding.RuleId), out ProblemCatalogEntry entry),
                        Is.True, $"{raiser} raises '{finding.RuleId}', which needs an entry");

                    Assert.That(finding.Message, Is.EqualTo(entry.MessageTemplate),
                        $"{raiser} raises '{finding.RuleId}' with a sentence its entry does not carry. The entry "
                        + "is the truth; copy it. If the template cannot be true of this raiser's condition, the "
                        + "code is carrying two conditions and needs SPLITTING, not an exception");
                }
            });
        }

        /// <summary>
        /// The English survived the move rather than being deleted. A Danish label is short by design, so the
        /// diagnostic is the only text naming the attribute, tag or enumeration that caused the finding.
        /// <para>
        /// Presence and distinctness, NOT equality with the entry's diagnostic — the two halves of the pair are
        /// asymmetric on purpose. A Danish <see cref="ProblemCatalogEntry.MessageTemplate"/> is a fixed label,
        /// so the raiser can copy it verbatim and the gate above can demand it back. The entry's diagnostic is an
        /// unbound TEMPLATE, while the advisor BINDS its values in — <c>#REQUIRED attribute 'id' is missing on
        /// &lt;product_dataline&gt;</c> against the entry's general sentence — so equality here would be false of
        /// six of the eleven raisers, and true only of the ones that happen to name nothing.
        /// </para>
        /// </summary>
        [Test]
        public void TheEnglishSentenceSurvivesAsTheDiagnostic()
        {
            Assert.Multiple(() =>
            {
                foreach ((string raiser, ProjectValidationFinding finding) in DefinitionFindingProbe.Provoked())
                {
                    Assert.That(finding.Diagnostic, Is.Not.Null.And.Not.Empty,
                        $"{raiser}'s '{finding.RuleId}' lost its English sentence instead of relocating it");
                    Assert.That(finding.Diagnostic, Is.Not.EqualTo(finding.Message),
                        $"{raiser}'s '{finding.RuleId}' says the same thing twice");
                }
            });
        }

        /// <summary>
        /// The point of the whole change, end to end: the aggregate a refused build throws is what a user
        /// actually meets, and its items now read in Danish. Both texts travel — the Danish sentence as the
        /// message, the English as the diagnostic — so a screen and a log each get the one written for it.
        /// </summary>
        [Test]
        public void TheRefusalAggregateCarriesTheDanishSentence()
        {
            ProjectValidationResult refused = ProductDefinitionBuilder
                .Dataline("_0x9fe2", "Drift probe").AddScenes("Scener").Validate();
            ProjectValidationException thrown = new(OperationCodes.ImportCatalog, refused);

            Problem item = thrown.Problems.Items.Single(i => i.Code.Value == "scenes-without-output");

            Assert.Multiple(() =>
            {
                Assert.That(refused.IsValid, Is.False, "precondition: this is a blocking finding");
                Assert.That(item.Message, Is.EqualTo("Scener uden udgang"),
                    "the sentence a user reads, in the language the application speaks");
                Assert.That(item.Diagnostic, Does.StartWith("AddScenes needs a preceding resource"),
                    "and the English detail is beside it rather than gone");
            });
        }
    }
}
