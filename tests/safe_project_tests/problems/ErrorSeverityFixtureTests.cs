using System;
using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The corpus is overwhelmingly WARNING material, and that is a measurement rather than an accident: an
    /// authentic vendor file is a file IHC Visual agreed to write, so the states the serializer or the controller
    /// would reject are exactly the ones a vendor tool does not produce.
    ///
    /// <para>That leaves a hole for anything that has to observe an <see cref="ValidationSeverity.Error"/> row
    /// end to end — a presentation surface with an error tier, an upload gate keyed on
    /// <see cref="ProjectValidationResult.IsValid"/>. Such a consumer needs a fixture it can point at and get an
    /// Error back from, and it needs that fixture PINNED, because "the file still carries an error" is otherwise
    /// an assumption its own tests inherit silently.</para>
    ///
    /// <para><c>Synthetic/DuplicatedAdressErrors.vis</c> is that fixture: a hand-built tree carrying three
    /// duplicate data-line address groups — two inputs on 1.01, two more on 1.02, two outputs on 1.01 — against
    /// <c>dataline-address-duplicate</c>, an ACTIVE Error row. This test is the pin; it says nothing about how
    /// many errors, only that the file is Error material at all, so a later rule change that legitimately moves
    /// the count does not move this gate.</para>
    /// </summary>
    [TestFixture]
    public sealed class ErrorSeverityFixtureTests
    {
        /// <summary>The synthetic project whose whole purpose is to carry duplicate data-line addresses.</summary>
        private const string ErrorFixture = "projects/Synthetic/DuplicatedAdressErrors.vis";

        private static Project Load(string relativePath)
        {
            using MemoryStream stream = new(TestData.ReadBytes(relativePath));
            return new ProjectAppService(TestSetup.Settings).Load(stream).GetAwaiter().GetResult();
        }

        [Test]
        public void TheDuplicateAddressFixtureIsErrorMaterialSoAnErrorTierCanBeObserved()
        {
            EquatableArray<ValidationFinding> findings =
                new ProjectAppService(TestSetup.Settings).ValidateStructured(Load(ErrorFixture));

            ValidationFinding[] errors = [.. findings.Where(f => f.Severity == ValidationSeverity.Error)];

            Assert.Multiple(() =>
            {
                Assert.That(findings, Is.Not.Empty, "the fixture must validate at all, or this pin is vacuous");
                Assert.That(errors, Is.Not.Empty,
                    "no Error finding: the only fixture a consumer can point at for an Error tier stopped " +
                    "producing one. Codes seen: " +
                    string.Join(", ", findings.Select(f => f.Code.Value).Distinct().Order()));
            });
        }

        /// <summary>
        /// The primary path, asserted separately from the tier itself. If the duplicate-address rule ever stops
        /// firing here but some other Error takes its place, the pin above stays green on purpose — a consumer
        /// only needs AN error — and this test is what says which one it used to be.
        /// </summary>
        [Test]
        public void TheFixturesErrorIsTheDuplicateDataLineAddressRow()
        {
            EquatableArray<ValidationFinding> findings =
                new ProjectAppService(TestSetup.Settings).ValidateStructured(Load(ErrorFixture));

            string[] errorCodes =
                [.. findings.Where(f => f.Severity == ValidationSeverity.Error).Select(f => f.Code.Value).Distinct()];

            Assert.That(errorCodes, Does.Contain("dataline-address-duplicate"));
        }

        /// <summary>
        /// And the flat door agrees: a consumer gating on <see cref="ProjectValidationResult.IsValid"/> — the
        /// upload path does — must see this file as invalid, not merely as one carrying findings.
        /// </summary>
        [Test]
        public void TheFlatResultReportsTheFixtureInvalidBecauseOnlyErrorBlocks()
        {
            ProjectValidationResult result = new ProjectAppService(TestSetup.Settings).Validate(Load(ErrorFixture));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.False);
                // Three today — one per duplicate group — but the pin is deliberately "not empty": the count is
                // a rule's business, and this gate exists so a consumer can observe AN Error tier.
                Assert.That(result.Errors, Is.Not.Empty);
                Assert.That(result.Infos, Is.Empty, "the Info tier ships empty: no rule emits it yet");
            });
        }
    }
}
