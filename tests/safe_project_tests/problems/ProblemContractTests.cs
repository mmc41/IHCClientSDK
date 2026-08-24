using System;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The uniform coded-problem contract (D6/R12/D14): a stable family-scoped <see cref="ProblemCode"/>, a
    /// user-facing DANISH message, typed structured arguments, and a SEPARATE English diagnostic detail with an
    /// optional originating exception.
    ///
    /// <para><b>What these tests hold, and why each matters.</b></para>
    /// <list type="bullet">
    /// <item><description><b>Family is read off the code, not stored beside it.</b> D09's owner rule has to be
    /// recoverable from the code alone, because R17's fitness scan and T044's ownership check both read it. A
    /// stored family would let a code and its family disagree.</description></item>
    /// <item><description><b>An unknown prefix degrades; it never throws.</b> R14's unknown-code rule: a host
    /// built against 0.8 meeting a family 0.9 introduced shows the message and groups it under
    /// <see cref="ProblemFamily.Unknown"/>. This is the whole of T008's degradation mechanism — one property, not
    /// a lifecycle class.</description></item>
    /// <item><description><b>The vocabulary stays OPEN.</b> The constructor is public and consults nothing — no
    /// catalogue, no registry — because D7 requires a host to build its own <c>app.*</c> problems. A gate here
    /// would close the vocabulary by the back door.</description></item>
    /// <item><description><b>Danish and English never share a slot.</b> ARCHITECTURE.md invariant 10. The Danish
    /// short fixed label is the message; the English engine sentence is the diagnostic; D18's translation of the
    /// 27 structural findings is exactly a move from the first slot to the second.</description></item>
    /// <item><description><b>Arguments are DATA.</b> The type vocabulary deliberately declares no prose kind, so
    /// "the message carries a sentence fragment of the source language" is unrepresentable rather than merely
    /// discouraged — which is the one cheap constraint that keeps translation possible later.</description></item>
    /// </list>
    /// </summary>
    [TestFixture]
    public sealed class ProblemContractTests
    {
        [Test]
        public void ProblemCode_ReadsItsFamilyFromTheDottedPrefix()
        {
            Assert.Multiple(() =>
            {
                // The validation family is the bare kebab-case catalogue ids, unchanged — the ONE family with no prefix.
                Assert.That(new ProblemCode("id-duplicate-token").Family, Is.EqualTo(ProblemFamily.Validation));
                Assert.That(new ProblemCode("load-empty").Family, Is.EqualTo(ProblemFamily.Validation));

                Assert.That(new ProblemCode("edit.target-missing").Family, Is.EqualTo(ProblemFamily.Edit));
                Assert.That(new ProblemCode("io.load").Family, Is.EqualTo(ProblemFamily.Io));
                Assert.That(new ProblemCode("import.catalog").Family, Is.EqualTo(ProblemFamily.Import));
                Assert.That(new ProblemCode("bridge.upload").Family, Is.EqualTo(ProblemFamily.Bridge));
                Assert.That(new ProblemCode("internal.unexpected").Family, Is.EqualTo(ProblemFamily.Internal));
                Assert.That(new ProblemCode("app.openvisual.recovery-declined").Family, Is.EqualTo(ProblemFamily.App));
            });
        }

        [Test]
        public void ProblemCode_OwnershipIsTrueExactlyForTheAppFamily()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new ProblemCode("app.openvisual.recovery-declined").IsHostOwned, Is.True);
                Assert.That(new ProblemCode("app.something").IsHostOwned, Is.True);

                Assert.That(new ProblemCode("internal.unexpected").IsHostOwned, Is.False);
                Assert.That(new ProblemCode("id-duplicate-token").IsHostOwned, Is.False);
                // Not a prefix match: "application" is its own (unknown) family, not the host family.
                Assert.That(new ProblemCode("application.thing").IsHostOwned, Is.False);
            });
        }

        [Test]
        public void ProblemCode_DegradesOnAnUnrecognisedPrefixRatherThanThrowing()
        {
            ProblemCode future = new("telemetry.export-refused");

            Assert.Multiple(() =>
            {
                Assert.That(future.Family, Is.EqualTo(ProblemFamily.Unknown));
                Assert.That(future.IsHostOwned, Is.False);
                Assert.That(future.Value, Is.EqualTo("telemetry.export-refused"), "the code itself survives intact");
                // default(ProblemCode) is reachable through an uninitialised field or an array; it must read, not throw.
                Assert.That(default(ProblemCode).Family, Is.EqualTo(ProblemFamily.Unknown));
            });
        }

        [Test]
        public void ProblemCode_ParseAcceptsAWellFormedCodeAndRejectsAMalformedOne()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ProblemCode.Parse("id-duplicate-token").Value, Is.EqualTo("id-duplicate-token"));
                Assert.That(ProblemCode.Parse("app.openvisual.recovery-declined").Family, Is.EqualTo(ProblemFamily.App));

                Assert.That(ProblemCode.TryParse("io.load", out ProblemCode load), Is.True);
                Assert.That(load.Value, Is.EqualTo("io.load"));

                // Shape violations: empty segment, upper case, a space, an unknown family prefix.
                Assert.That(ProblemCode.TryParse("io..load", out _), Is.False);
                Assert.That(ProblemCode.TryParse("Io.Load", out _), Is.False);
                Assert.That(ProblemCode.TryParse("io.load empty", out _), Is.False);
                Assert.That(ProblemCode.TryParse("telemetry.export-refused", out _), Is.False);
                Assert.That(ProblemCode.TryParse("-leading-dash", out _), Is.False);
                Assert.That(ProblemCode.TryParse(string.Empty, out _), Is.False);

                Assert.That(() => ProblemCode.Parse("Io.Load"), Throws.ArgumentException);
            });
        }

        /// <summary>
        /// The positional constructor is deliberately NOT gated by <see cref="ProblemCode.Parse"/>: D7's open
        /// vocabulary needs a constructor a host can call. The consequence, stated here so nothing downstream
        /// assumes otherwise, is that a code may be malformed and every reader must cope.
        /// </summary>
        [Test]
        public void ProblemCode_ConstructorDoesNotValidate_SoNoReaderMayAssumeAWellFormedCode()
        {
            ProblemCode malformed = new("nonsense!!");

            Assert.Multiple(() =>
            {
                Assert.That(malformed.Value, Is.EqualTo("nonsense!!"));
                Assert.That(malformed.Family, Is.EqualTo(ProblemFamily.Unknown));
                Assert.That(ProblemCode.TryParse("nonsense!!", out _), Is.False, "Parse still rejects it");
            });
        }

        [Test]
        public void ProblemCode_ExplanationAnchorIsDerivedFromTheCode()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new ProblemCode("id-duplicate-token").ExplanationAnchor, Is.EqualTo("id-duplicate-token"));
                Assert.That(new ProblemCode("io.load").ExplanationAnchor, Is.EqualTo("io-load"));
                Assert.That(new ProblemCode("app.openvisual.recovery-declined").ExplanationAnchor,
                    Is.EqualTo("app-openvisual-recovery-declined"));
            });
        }

        [Test]
        public void Problem_KeepsTheDanishMessageAndTheEnglishDiagnosticInSeparateSlots()
        {
            Problem problem = new(
                new ProblemCode("io.load"),
                "Projektet kunne ikke åbnes.",
                EquatableArray.Create<ProblemArgument>([new ProblemArgument("path", @"C:\projekter\hus.vis")]),
                Diagnostic: "The stream ended before the root element was closed.");

            Assert.Multiple(() =>
            {
                Assert.That(problem.Message, Is.EqualTo("Projektet kunne ikke åbnes."));
                Assert.That(problem.Diagnostic, Is.EqualTo("The stream ended before the root element was closed."));
                Assert.That(problem.Message, Does.Not.Contain(problem.Diagnostic!),
                    "invariant 10: the engine sentence never leaks into the user-facing slot");
                Assert.That(problem.Cause, Is.Null);
            });
        }

        [Test]
        public void Problem_UnexpectedIsTheNamedSdkCatchAll()
        {
            InvalidOperationException cause = new("index rebuild failed");
            Problem problem = Problem.Unexpected("The element index could not be rebuilt after the commit.", cause);

            Assert.Multiple(() =>
            {
                Assert.That(problem.Code.Value, Is.EqualTo("internal.unexpected"));
                Assert.That(problem.Code.Family, Is.EqualTo(ProblemFamily.Internal));
                Assert.That(problem.Diagnostic, Is.EqualTo("The element index could not be rebuilt after the commit."));
                Assert.That(problem.Cause, Is.SameAs(cause));
                Assert.That(problem.Arguments.IsEmpty, Is.True);
                // A short FIXED Danish label, per the catalogue convention — not the English diagnostic, and not
                // a sentence assembled from it.
                Assert.That(problem.Message, Is.EqualTo("Uventet fejl"));
            });
        }

        /// <summary>
        /// The argument type vocabulary carries no prose kind. An argument holding a word or sentence fragment of
        /// the source language would make the template untranslatable — the fragment would need translating too,
        /// and nothing would know to. This test is what keeps that structural rather than remembered.
        /// </summary>
        [Test]
        public void ProblemArguments_AreDataOnly_TheTypeVocabularyDeclaresNoProseKind()
        {
            string[] names = Enum.GetNames<ProblemArgumentType>();

            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EquivalentTo(new[]
                {
                    nameof(ProblemArgumentType.ElementIdentity),
                    nameof(ProblemArgumentType.SchemaName),
                    nameof(ProblemArgumentType.AuthoredName),
                    nameof(ProblemArgumentType.Integer),
                    nameof(ProblemArgumentType.Number),
                    nameof(ProblemArgumentType.AttributeValue),
                    nameof(ProblemArgumentType.Path),
                }));

                foreach (string prose in new[] { "Sentence", "Phrase", "Label", "Text", "Message" })
                {
                    Assert.That(names, Does.Not.Contain(prose),
                        $"a '{prose}' argument kind would make every template carrying it untranslatable");
                }
            });
        }

        [Test]
        public void Problem_ArgumentsKeepTheirDeclaredOrderAndCompareByValue()
        {
            ProblemArgumentSlot[] slots =
            [
                new ProblemArgumentSlot("element", ProblemArgumentType.ElementIdentity),
                new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
            ];

            EquatableArray<ProblemArgument> bound = EquatableArray.Create<ProblemArgument>(
                [new ProblemArgument("element", "_0x2a"), new ProblemArgument("limit", 64)]);

            Problem first = new(new ProblemCode("capacity-wireless-exceeded"), "For mange trådløse enheder", bound);
            Problem second = new(new ProblemCode("capacity-wireless-exceeded"), "For mange trådløse enheder", bound);

            Assert.Multiple(() =>
            {
                Assert.That(bound.Select(a => a.Name), Is.EqualTo(slots.Select(s => s.Name)).AsCollection,
                    "arguments are bound in declared slot order");
                Assert.That(first, Is.EqualTo(second), "record equality reaches through the argument array");
                Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            });
        }

        /// <summary>
        /// D7's open vocabulary, exercised: a host mints a code in its reserved family and builds the problem
        /// itself, with no SDK registry consulted and nothing to register with.
        /// </summary>
        [Test]
        public void Problem_IsHostConstructible_WithNoRegistryToConsult()
        {
            Problem hostProblem = new(
                ProblemCode.Parse("app.openvisual.recovery-declined"),
                "Gendannelse afvist",
                EquatableArray<ProblemArgument>.Empty,
                Diagnostic: "The user declined the crash-recovery prompt.");

            Assert.Multiple(() =>
            {
                Assert.That(hostProblem.Code.IsHostOwned, Is.True);
                Assert.That(hostProblem.Code.Family, Is.EqualTo(ProblemFamily.App));
                Assert.That(hostProblem.Message, Is.EqualTo("Gendannelse afvist"));
            });
        }
    }
}
