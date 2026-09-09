using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    /// <summary>
    /// The SDK boundary fitness test, built against the written specification in
    /// <c>ihcclient/docs/api-contract.md</c> section 7. One question: does any type the supported API hands a
    /// caller, or asks a caller for, fall outside the approved contract set?
    /// <para>
    /// The seeds are ENUMERATED, never derived from a namespace — a namespace-derived seed set silently
    /// re-approves whatever is added to that namespace next, which is the failure of the reachability calculation
    /// this replaces. Everything the traversal then reaches must appear in <see cref="ApprovedContracts"/>, and a
    /// type that does not is a finding about the member that exposed it: a public member returning an
    /// implementation type does not approve that type.
    /// </para>
    /// <para>
    /// What this does NOT cover is in the same specification: member-level narrowing on an approved type,
    /// behavioural contract (snapshot ownership, event threading, cancellation), and consumer rules.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SdkBoundaryArchitectureTests
    {
        /// <summary>
        /// The seeds: both supported service tiers, the client-facing ports they return, and the companion and
        /// provider contracts the specification approves by name.
        /// </summary>
        private static IEnumerable<Type> Seeds()
        {
            yield return typeof(global::Ihc.App.IIHCAppService);

            // The API-service tier: every implementation of the marker, which is the tier's own definition -- the
            // marker interface itself included, since a type is assignable to itself.
            foreach (Type api in SdkAssembly.GetTypes()
                         .Where(t => t.IsVisible && typeof(global::Ihc.IIHCApiService).IsAssignableFrom(t)))
            {
                yield return api;
            }

            // The application-service tier.
            yield return typeof(global::Ihc.Vis.ProjectAppService);
            yield return typeof(global::Ihc.App.AdminAppService);
            yield return typeof(global::Ihc.App.InformationAppService);
            yield return typeof(global::Ihc.App.LabAppService);

            // The client-facing ports and the provider contract a client may implement.
            yield return typeof(global::Ihc.Vis.IProjectDocument);
            yield return typeof(global::Ihc.Vis.ProjectCommands);
            yield return typeof(global::Ihc.Vis.IReportIconProvider);

            // The documented failure contracts. Seeded because no signature can reach them -- an exception is
            // thrown, never returned -- so without naming them here their own surfaces would go unchecked, which is
            // the one part of the contract a signature traversal cannot discover for itself.
            yield return typeof(global::Ihc.Vis.Io.ProjectFormatException);
            yield return typeof(global::Ihc.Vis.Problems.RefusedOperationException);
            yield return typeof(global::Ihc.Vis.Problems.RefusedWriteException);
            yield return typeof(global::Ihc.Vis.Problems.RefusedImportException);
            yield return typeof(global::Ihc.Vis.Session.EditRefusedException);
            yield return typeof(global::Ihc.Vis.Validation.ProjectValidationException);
            yield return typeof(global::Ihc.Vis.ProjectUploadException);

            // The read surface a caller reaches WITHOUT holding a service: the extension holders over Project and
            // ProjectElement, and the finding helpers. ARCHITECTURE.md admits these as supported ("supported static
            // helpers and extension methods may be called directly"), and nothing seeded above names them -- an
            // extension is reached through the type it extends, so a seed set of entry points alone leaves this
            // whole surface unmeasured.
            yield return typeof(global::Ihc.Vis.ProjectReadView);
            yield return typeof(global::Ihc.Vis.ProjectElementRead);
            yield return typeof(global::Ihc.Vis.ProjectProjections);
            yield return typeof(global::Ihc.Vis.Validation.ValidationGate);
        }

        /// <summary>
        /// Every type the seeds' public signatures reach, expanded recursively through returns, parameters,
        /// properties, fields, events, constructors, base types, interfaces, generic arguments and constraints.
        /// </summary>
        // Traversed ONCE for the fixture: three tests ask for it, and each pass re-walks every SDK type looking for
        // the API tier before expanding the whole signature closure.
        private static readonly Lazy<IReadOnlyCollection<Type>> Reached = new(TraverseSdkTypes);

        private static IReadOnlyCollection<Type> ReachedSdkTypes() => Reached.Value;

        private static IReadOnlyCollection<Type> TraverseSdkTypes()
        {
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>(Seeds());
            while (queue.Count > 0)
            {
                Type current = queue.Dequeue();
                if (!seen.Add(current))
                    continue;
                foreach (Type reached in ContractSurfaceTypes(current))
                    if (reached.Assembly == SdkAssembly && !seen.Contains(reached))
                        queue.Enqueue(reached);
            }

            // Normalised to the generic DEFINITION: `EquatableArray<Finding>` and `EquatableArray<string>` are the
            // same contract, and a roster keyed on constructed instances would grow a row per element type.
            return [.. seen
                .Select(t => t.IsArray && t.GetElementType() is { } element ? element : t)
                .Select(t => t.IsConstructedGenericType ? t.GetGenericTypeDefinition() : t)
                // A generic PARAMETER belongs to no assembly surface of its own -- its constraints were already
                // expanded -- and has no full name to approve, so it is not a reachable contract type.
                .Where(t => !t.IsGenericParameter && t.FullName is not null)
                .Where(t => t.Assembly == SdkAssembly && t.IsVisible && !IsBoundedTechnicalException(t))
                .Distinct()];
        }

        /// <summary>The generated SOAP layer and the envelope roots, excluded by the same names the repository's
        /// consumer bans use — the bounded technical exceptions of the specification's section 5, which are public
        /// because XmlSerializer binds public types and are bounded by a ban rather than by accessibility.</summary>
        private static bool IsBoundedTechnicalException(Type type) =>
            IsInNamespaceSubtree(type.Namespace, "Ihc.Soap") || IsInNamespaceSubtree(type.Namespace, "Ihc.Envelope");

        /// <summary>Every type a contract's inherited public surface names, expanded recursively. The traversal is
        /// the shared one, so this scan and the narrower port scan cannot disagree about what a surface names.</summary>
        private static IEnumerable<Type> ContractSurfaceTypes(Type contract) =>
            ContractHierarchy(contract)
                .SelectMany(DeclaredContractTypes)
                .SelectMany(TypeAndArguments)
                .Distinct();

        [Test]
        public void EverySdkTypeTheSupportedApiExposes_IsAnApprovedContract()
        {
            string[] unapproved = [.. ReachedSdkTypes()
                .Select(t => t.FullName!)
                .Where(name => !ApprovedContracts.Contains(name))
                .Order(StringComparer.Ordinal)];

            Assert.That(unapproved, Is.Empty,
                "each of these is reached from a supported entry point through a public signature but is not an "
                + "approved contract. A public member returning an implementation type does not approve it: narrow "
                + "the member, return an approved type, or add the type to ApprovedContracts as a reviewed decision. "
                + "Unapproved: " + string.Join(", ", unapproved));
        }

        /// <summary>
        /// The vacuity guard. A structural test that traverses nothing looks exactly like a clean surface, so the
        /// traversal is asserted to have observed real contract types from the live surface before its verdict means
        /// anything.
        /// </summary>
        [Test]
        public void TheTraversalObservesTheLiveSurface()
        {
            var reached = ReachedSdkTypes();

            Assert.Multiple(() =>
            {
                Assert.That(reached, Has.Count.GreaterThan(50),
                    "the supported surface reaches far more than a handful of types — a small count means the "
                    + "traversal stopped early and its verdict is meaningless");
                foreach (Type expected in new[]
                         {
                             typeof(global::Ihc.Vis.Projects.Project),
                             typeof(global::Ihc.Vis.Model.ElementId),
                             typeof(global::Ihc.Vis.Session.EditOutcome),
                         })
                {
                    Assert.That(reached, Does.Contain(expected),
                        $"{expected.Name} is on the live supported surface and must be observed");
                }
            });
        }

        /// <summary>
        /// The name-resolution guard the specification requires for every hand-maintained entry: an approved name
        /// that no longer resolves is a roster silently shrinking, which reads as a cleaner surface rather than as
        /// the rename it is.
        /// </summary>
        [Test]
        public void EveryApprovedNameStillResolves()
        {
            string[] missing = [.. ApprovedContracts
                .Where(name => SdkAssembly.GetType(name, throwOnError: false) is null)
                .Order(StringComparer.Ordinal)];

            Assert.That(missing, Is.Empty,
                "an approved contract name no longer resolves in the SDK — a rename must move the entry, not leave "
                + "the roster quietly stale: " + string.Join(", ", missing));
        }

        /// <summary>
        /// The positive control. The same checker, pointed at synthetic contracts that leak a non-approved type
        /// through a RETURN, a PARAMETER and a GENERIC ARGUMENT, must report all three — proving the traversal
        /// inspects each shape rather than passing because it inspected none.
        /// </summary>
        [Test]
        public void TheBoundaryScanIsArmed()
        {
            string[] reached = [.. new[] { typeof(ISeededLeakyContract<>) }
                .SelectMany(ContractSurfaceTypes)
                .Select(t => t.FullName!)
                .Distinct()];

            Assert.Multiple(() =>
            {
                Assert.That(reached, Does.Contain(typeof(SeededUnapprovedReturn).FullName),
                    "a leak through a RETURN must be seen");
                Assert.That(reached, Does.Contain(typeof(SeededUnapprovedParameter).FullName),
                    "a leak through a PARAMETER must be seen");
                Assert.That(reached, Does.Contain(typeof(SeededUnapprovedConstraint).FullName),
                    "a leak through a GENERIC CONSTRAINT must be seen");
            });
        }

        // The synthetic violators: one per shape the traversal must cover. They live in the TEST assembly, so they
        // can never reach the production scan — which walks the SDK assembly only — while being run through the very
        // same traversal.
        private interface ISeededLeakyContract<T>
            where T : SeededUnapprovedConstraint
        {
            SeededUnapprovedReturn Leaked { get; }

            void Accepts(SeededUnapprovedParameter parameter);
        }

        private sealed class SeededUnapprovedReturn { }

        private sealed class SeededUnapprovedParameter { }

        // An INTERFACE, not a class: a constraint type must be an interface, a non-sealed class or a type
        // parameter, and a non-sealed class here would trip CA1852. The leak shape is the same either way -- what
        // the traversal must see is the type NAMED as a constraint.
        private interface SeededUnapprovedConstraint { }

        /// <summary>
        /// The approved contract set: every SDK type a supported entry point may hand a caller or ask a caller for.
        /// Hand-maintained on purpose — adding a name is a reviewed decision a reader sees in the diff, which is the
        /// property a derived set cannot have.
        /// <para>
        /// Kept in a checked-in FILE rather than a C# literal for the same reason: a roster this size reads as noise
        /// inside a fixture, while as a sorted file each addition is one line in a review.
        /// </para>
        /// </summary>
        private static readonly IReadOnlySet<string> ApprovedContracts = new HashSet<string>(
            System.IO.File.ReadAllLines(RosterPath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal)),
            StringComparer.Ordinal);

        private static string RosterPath =>
            System.IO.Path.Combine(Ihc.Tests.Shared.TestRepository.RequireRoot(),
                "tests", "safe_architecture_tests", "ApprovedSdkContracts.txt");
    }
}
