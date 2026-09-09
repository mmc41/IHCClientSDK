using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Domain;
using NUnit.Framework;
using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    /// <summary>
    /// The `ihc_lab` friend grant is RETAINED (owner decision 8), and this is what keeps it honest: a grant is
    /// blanket by construction — it opens every internal in the SDK to one assembly — so what bounds it is not the
    /// grant but a check that says which internals Lab may actually reach.
    /// <para>
    /// Two are documented and allowed. `ValidationHelper.ValidateDataAnnotations` is the SDK's own DataAnnotations
    /// validation, which Lab's setup screen runs over the settings a user types; `ServiceMetadata.TryGetCollectionElementType`
    /// is an internal MEMBER of a PUBLIC type, which is exactly the shape a type-level sweep cannot see — the type
    /// is public, so nothing about the reference looks unusual until the member is resolved.
    /// </para>
    /// <para>
    /// Anything else Lab reaches is a finding, and the fix is a supported member on the SDK rather than a wider
    /// grant. Armed by <see cref="TheScanSeesTheDocumentedReaches"/>: if the detector stopped resolving members it
    /// would report nothing and read as a perfectly bounded grant.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LabFriendGrantArchitectureTests
    {
        private const string LabRoot = "IhcLab";

        private static Architecture Lab => ArchitectureModels.Lab;

        /// <summary>The internals Lab is allowed to reach, as (declaring type, member) pairs.</summary>
        private static readonly IReadOnlySet<(string Type, string Member)> AllowedReaches =
            new HashSet<(string, string)>
            {
                ("Ihc.ValidationHelper", "ValidateDataAnnotations"),
                ("Ihc.ServiceMetadata", "TryGetCollectionElementType"),
            };

        [Test]
        public void Lab_ReachesOnlyItsTwoDocumentedInternals()
        {
            var reaches = InternalReaches();

            Assert.That(reaches.Select(r => (r.TargetType, r.Member)).Distinct().Except(AllowedReaches), Is.Empty,
                "the ihc_lab friend grant is bounded to its two documented dependencies: a new internal reach means "
                + "the SDK is missing a supported member, not that the grant should widen — offending calls: "
                + string.Join("; ", reaches
                    .Where(r => !AllowedReaches.Contains((r.TargetType, r.Member)))
                    .Select(r => $"{r.Origin}.{r.OriginMember} -> {r.TargetType}.{r.Member}")));
        }

        /// <summary>
        /// The arming control. Both documented reaches must be OBSERVED, which is what proves the scan resolves
        /// members and recognises non-public ones — a detector that silently resolved nothing would satisfy the rule
        /// above while checking nothing at all.
        /// </summary>
        [Test]
        public void TheScanSeesTheDocumentedReaches()
        {
            var observed = InternalReaches().Select(r => (r.TargetType, r.Member)).ToHashSet();

            Assert.Multiple(() =>
            {
                Assert.That(LabEdges.Value, Is.Not.Empty,
                    "no method-call edges were seen at all — the call detection is not working");
                foreach ((string type, string member) in AllowedReaches)
                    Assert.That(observed, Does.Contain((type, member)),
                        $"the documented reach {type}.{member} must be observed, or this scan is not seeing internals");
            });
        }

        /// <summary>
        /// Every call Lab makes into a non-public SDK member. "Non-public" is asked of the RESOLVED member, not of
        /// the declaring type: an internal member on a public type is the case a type-level sweep misses, and it is
        /// one of the two this grant exists for.
        /// </summary>
        private static IReadOnlyList<(string Origin, string OriginMember, string TargetType, string Member)> InternalReaches() =>
            Reaches.Value;

        // Both tests ask the same question of the same assembly, and the edge walk re-reads every type's
        // dependencies each time it runs. Declared before Reaches, which reads it.
        private static readonly Lazy<IReadOnlyList<(string Origin, string OriginMember, string TargetType, string Member)>> LabEdges =
            new(() => MethodCallEdges(Lab, LabRoot));

        private static readonly Lazy<IReadOnlyList<(string Origin, string OriginMember, string TargetType, string Member)>> Reaches =
            new(() => [.. LabEdges.Value.Where(edge => ReachesNonPublicSdkMember(edge.TargetType, edge.Member))]);

        private static readonly Dictionary<(string Type, string Member), bool> NonPublicReach = [];

        private static bool ReachesNonPublicSdkMember(string targetTypeFullName, string memberName)
        {
            // Memoised: the same declaring type appears on many edges, and each miss costs a type lookup plus a
            // full member array.
            if (NonPublicReach.TryGetValue((targetTypeFullName, memberName), out bool cached))
                return cached;

            bool reaches = ResolveNonPublicReach(targetTypeFullName, memberName);
            NonPublicReach[(targetTypeFullName, memberName)] = reaches;
            return reaches;
        }

        private static bool ResolveNonPublicReach(string targetTypeFullName, string memberName)
        {
            Type? target = SdkAssembly.GetType(targetTypeFullName, throwOnError: false);
            if (target is null)
                return false;   // not an SDK type at all

            if (!target.IsVisible)
                return true;    // an internal TYPE: every member of it is a non-public reach

            MethodInfo[] overloads = [.. target
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name == memberName)];

            // A member the SDK declares only non-publicly. Asked over every overload, because a name with one public
            // overload is reachable and says nothing about the grant.
            return overloads.Length > 0 && overloads.All(m => !m.IsPublic);
        }
    }
}
