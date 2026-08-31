using System;
using System.Linq;
using System.Reflection;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The ownership partition, asserted as properties of the surface rather than enforced by a registry.
    ///
    /// <para><b>Why there is no registry.</b> The design first carried an owner-aware code registry — a registry,
    /// a builder, a declaration record, a rejection enum and an exception — and the audits removed the whole
    /// stack: it gated nothing at construction time by its own documentation, restated the catalogue entry's
    /// fields, and duplicated the catalogue's lookup and duplicate-code check. Its four substantive requirements
    /// survive elsewhere, and this fixture is where the two that are properties of the problem contract are
    /// pinned:</para>
    /// <list type="number">
    /// <item><description><b>Construction consults nothing</b> — a host builds a problem for its own reserved
    /// family with no SDK type to register with. Pinned here as the absence of any gate in the namespace, which
    /// is stronger than a test that one construction happens to succeed.</description></item>
    /// <item><description><b>Ownership has exactly one answer</b> — one predicate, derived from the code alone,
    /// so an ownership scan cannot read a second, disagreeing source.</description></item>
    /// </list>
    /// <para>The other two are checked where the artifacts they are about live: a duplicate code is a catalogue
    /// invariant, and the SDK catch-all is a catalogue entry — neither exists until the catalogue does. A
    /// completeness invariant over the catalogue is what catches either of them being missed.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProblemOwnershipTests
    {
        private static Type[] PublicProblemTypes() =>
            typeof(Problem).Assembly.GetExportedTypes()
                .Where(t => t.Namespace == typeof(Problem).Namespace)
                .ToArray();

        /// <summary>
        /// The whole public surface of the problem contract, enumerated. It matters that this is short and that
        /// nothing in it can refuse a construction: an open vocabulary is what lets a host mint its own codes,
        /// and a gate added here later would close it by the back door without anyone deciding to.
        /// <para>
        /// THE REFUSAL CARRIERS are here rather than in a layer because more
        /// than one layer raises the same operation: a save is refused by the serializer, by a schema guard and
        /// by the atomic writer, which share no dependency. None of them gates anything — an identity is data a
        /// site hands over, and a host can build one for its own family with no SDK type consulted.
        /// </para>
        /// </summary>
        [Test]
        public void TheProblemContractIsExactlyTheseTypesAndNoneOfThemIsAGate()
        {
            string[] names = PublicProblemTypes().Select(t => t.Name).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EquivalentTo(new[]
                {
                    nameof(ProblemFamily),
                    nameof(ProblemCode),
                    nameof(ProblemCodeStatus),
                    nameof(ProblemArgumentType),
                    nameof(ProblemArgumentSlot),
                    nameof(ProblemArgument),
                    nameof(Problem),
                    // A fault in the TOOL is contract too, and deliberately NOT a finding: it is the one member
                    // of this namespace that describes the software rather than the project.
                    nameof(InternalError),
                    nameof(InternalErrorOrigin),
                    nameof(ProblemChain),
                    nameof(ProblemAggregate),
                    nameof(RefusalIdentity),
                    nameof(OperationCodes),
                    nameof(IProblemCarrier),
                    nameof(RefusedOperationException),
                    nameof(RefusedWriteException),
                    nameof(RefusedImportException),
                }));

                foreach (string gate in new[] { "Registry", "Builder", "Declaration", "Rejection", "Registration" })
                {
                    Assert.That(names, Has.None.Contains(gate),
                        $"a '{gate}' type here would gate construction and close the vocabulary to hosts");
                }
            });
        }

        /// <summary>
        /// Construction consults nothing: a host mints a code in its reserved family and builds the problem, with
        /// no registration step available to it and none required of it.
        /// </summary>
        [Test]
        public void AHostMintsItsOwnCodeWithNothingToRegisterWith()
        {
            ProblemCode hostCode = ProblemCode.Parse("app.openvisual.recovery-declined");
            Problem hostProblem = new(hostCode, "Gendannelse afvist", EquatableArray<ProblemArgument>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(hostProblem.Code, Is.EqualTo(hostCode));
                Assert.That(hostProblem.Code.IsHostOwned, Is.True);

                // Nothing on the constructed value points back at an SDK authority — no owner field, no source,
                // no catalogue reference. The code IS the whole of its provenance.
                Assert.That(typeof(Problem).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(p => p.Name),
                    Is.EquivalentTo(new[]
                    {
                        nameof(Problem.Code),
                        nameof(Problem.Message),
                        nameof(Problem.Arguments),
                        nameof(Problem.Diagnostic),
                        nameof(Problem.Cause),
                    }));
            });
        }

        /// <summary>
        /// One ownership predicate, agreeing with the family across the whole enum. A second predicate is the
        /// failure mode this guards: two answers that can disagree, and a scan that reads the wrong one.
        /// </summary>
        [Test]
        public void OwnershipHasExactlyOnePredicate_AndItAgreesWithTheFamily()
        {
            (string Code, ProblemFamily Family)[] representatives =
            [
                ("id-duplicate-token", ProblemFamily.Validation),
                ("edit.target-missing", ProblemFamily.Edit),
                ("io.load", ProblemFamily.Io),
                ("import.catalog", ProblemFamily.Import),
                ("bridge.upload", ProblemFamily.Bridge),
                ("internal.unexpected", ProblemFamily.Internal),
                ("app.openvisual.recovery-declined", ProblemFamily.App),
                ("telemetry.export-refused", ProblemFamily.Unknown),
            ];

            string[] members = typeof(ProblemCode)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(Enum.GetValues<ProblemFamily>().Select(f => f).Distinct().Count(),
                    Is.EqualTo(representatives.Length),
                    "every family has a representative below, so a new family cannot slip past this test");

                foreach ((string code, ProblemFamily family) in representatives)
                {
                    ProblemCode subject = new(code);
                    Assert.That(subject.Family, Is.EqualTo(family), code);
                    Assert.That(subject.IsHostOwned, Is.EqualTo(family == ProblemFamily.App), code);
                }

                foreach (string second in new[] { "Owner", "IsSdkOwned", "IsOwnedBy", "Ownership" })
                {
                    Assert.That(members, Has.None.Contains(second),
                        $"'{second}' would be a second ownership answer that can disagree with IsHostOwned");
                }
            });
        }

        /// <summary>
        /// The SDK's one hand-minted code is in the SDK's own catch-all family, never the reserved host one. Every
        /// other SDK code is minted from its catalogue entry, which is where the wider ownership scan applies.
        /// </summary>
        [Test]
        public void TheSdkCatchAllIsSdkOwned()
        {
            ProblemCode code = Problem.Unexpected("CommitEdit", "The element index could not be rebuilt.").Code;

            Assert.Multiple(() =>
            {
                Assert.That(code.Family, Is.EqualTo(ProblemFamily.Internal));
                Assert.That(code.IsHostOwned, Is.False);
            });
        }
    }
}
