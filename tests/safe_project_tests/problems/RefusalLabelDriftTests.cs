using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T063's drift gate: EVERY Danish sentence a refusing site carries is the same words as its catalogue entry's
    /// template — all of them, reflected, rather than the handful each family's own suite happened to sample.
    ///
    /// <para><b>The duplication is deliberate and cannot be removed.</b> A refusing site below the validation engine
    /// may not read the catalogue — the layer rules forbid it — so the words are necessarily written twice: once on
    /// the entry that governs the code, once on the <see cref="RefusalIdentity"/> the site raises. What CAN be
    /// removed is the risk: reflect every identity every registry exposes and require agreement, so an edit to
    /// either side fails the gate instead of shipping two spellings of one sentence.</para>
    ///
    /// <para><b>Why this replaces sampling.</b> The load family pinned four of its causes, the save family three,
    /// the mint factories three — roughly a quarter of the identities in the SDK. The rest were duplicated Danish
    /// text with nothing holding the two copies together, which is exactly the defect T063 sweeps: a string the
    /// master artifact now owns, living a second life where nothing checks it.</para>
    /// </summary>
    [TestFixture]
    public sealed class RefusalLabelDriftTests
    {
        /// <summary>Every refusal identity the SDK's registries expose, with the member it came from.</summary>
        private static ImmutableArray<(string Member, RefusalIdentity Identity)> Identities()
        {
            var found = ImmutableArray.CreateBuilder<(string, RefusalIdentity)>();
            foreach (Type registry in typeof(RefusalIdentity).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed))
            {
                foreach (PropertyInfo property in registry
                    .GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    object? value = property.PropertyType == typeof(RefusalIdentity)
                        || typeof(IEnumerable<RefusalIdentity>).IsAssignableFrom(property.PropertyType)
                            ? property.GetValue(null)
                            : null;

                    switch (value)
                    {
                        case RefusalIdentity identity:
                            found.Add(($"{registry.Name}.{property.Name}", identity));
                            break;

                        // A registry that also exposes an `All` collection is read through it too, so an identity
                        // reachable only from the collection is still governed and still checked.
                        case IEnumerable<RefusalIdentity> many:
                            found.AddRange(many.Select(i => ($"{registry.Name}.{property.Name}", i)));
                            break;

                        default:
                            break;
                    }
                }
            }

            return found.ToImmutable();
        }

        [Test]
        public void EveryRefusalIdentityIsGovernedByAnEntry()
        {
            ImmutableArray<(string Member, RefusalIdentity Identity)> identities = Identities();

            Assert.Multiple(() =>
            {
                // A COUNT would be a magic number; what matters is that every family a registry declares is
                // reachable by reflection. The LOAD family used to be absent here — its registry exposed bare
                // codes and its Danish labels were hand-typed at thirteen sites in `ProjectReader`, so nothing
                // reflective could reach them and its half had to be carried by LoadRefusalTests alone. It now
                // exposes whole identities like its three siblings, so the gate is universal.
                Assert.That(identities.Select(pair => pair.Identity.Operation.Value).Distinct(),
                    Is.SupersetOf(new[] { "io.load", "io.save", "import.catalog", "bridge.download", "bridge.upload" }),
                    "every family that declares whole identities must be reachable by reflection; a family that "
                    + "stopped being found would make this suite quiet rather than failing");
                foreach ((string member, RefusalIdentity identity) in identities)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(identity.Cause, out _), Is.True,
                        $"{member} raises {identity.Cause.Value}, which needs an entry");
                    Assert.That(ProblemCatalog.Current.TryGet(identity.Operation, out _), Is.True,
                        $"{member}'s operation head {identity.Operation.Value} needs an entry too");
                }
            });
        }

        /// <summary>
        /// The drift gate itself: cause label against cause template, operation label against operation template,
        /// for every identity in the SDK.
        /// </summary>
        [Test]
        public void EveryRefusalLabelIsItsEntrysTemplate()
        {
            Assert.Multiple(() =>
            {
                foreach ((string member, RefusalIdentity identity) in Identities())
                {
                    AssertAgrees(member, identity.Cause, identity.CauseLabel, "cause");
                    AssertAgrees(member, identity.Operation, identity.OperationLabel, "operation");
                }
            });
        }

        /// <summary>
        /// The armed control: the gate must FAIL a drifted spelling. It takes a real identity, drifts its Danish
        /// sentence by one character, and requires <see cref="Agrees"/> — the SAME predicate
        /// <see cref="EveryRefusalLabelIsItsEntrysTemplate"/> asserts through — to come out false for it.
        ///
        /// <para>Running the real predicate is the point. An earlier version of this control selected an entry
        /// with the predicate <c>e.MessageTemplate != identity.CauseLabel</c> and then asserted that those two
        /// were unequal, which is a tautology: it could only fail by <c>First</c> throwing, so it proved the
        /// registry was non-empty and nothing about the gate.</para>
        /// </summary>
        [Test]
        public void TheGateIsArmed()
        {
            (string member, RefusalIdentity identity) = Identities()
                .First(pair => pair.Identity.CauseLabel.Length > 0
                    && ProblemCatalog.Current.TryGet(pair.Identity.Cause, out ProblemCatalogEntry e)
                    && e.MessageTemplate.Length > 0);

            RefusalIdentity drifted = identity with { CauseLabel = identity.CauseLabel + " x" };

            Assert.Multiple(() =>
            {
                Assert.That(Agrees(identity.Cause, identity.CauseLabel), Is.True,
                    $"{member}: the undrifted identity must agree, or the control is measuring the wrong thing");
                Assert.That(Agrees(drifted.Cause, drifted.CauseLabel), Is.False,
                    $"{member}: a one-character drift must fail the same comparison the gate makes");
            });
        }

        private static void AssertAgrees(string member, ProblemCode code, string label, string half)
        {
            Assert.That(Agrees(code, label), Is.True,
                $"{member}: the {half} label and its catalogue template must be the same words — the raising site "
                + "cannot read the catalogue, so nothing but this test keeps them in step");
        }

        /// <summary>
        /// The gate's one comparison, as a predicate so the armed control can NEGATE it rather than restate it.
        /// </summary>
        /// <param name="code">The code whose entry governs the sentence.</param>
        /// <param name="label">The sentence the raising site carries.</param>
        private static bool Agrees(ProblemCode code, string label)
        {
            if (!ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry))
            {
                return true;   // the governance half is the other test's assertion
            }

            // An entry with no template of its own has nothing to drift from: the operation heads that carry only
            // a diagnostic are governed, not rendered.
            return entry.MessageTemplate.Length == 0
                || string.Equals(label, entry.MessageTemplate, StringComparison.Ordinal);
        }
    }
}
