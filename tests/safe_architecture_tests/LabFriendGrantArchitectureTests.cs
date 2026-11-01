using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArchUnitNET.Domain;
using NUnit.Framework;
using static Ihc.Tests.ArchRuleHelpers;
// ArchUnitNET.Domain also exports an `Assembly`; the constructor scan reads reflection assemblies.
using Assembly = System.Reflection.Assembly;

namespace Ihc.Tests
{
    /// <summary>
    /// The `ihc_lab` friend grant is RETAINED (owner decision 8), and this is what keeps it honest: a grant is
    /// blanket by construction — it opens every internal in the SDK to one assembly — so what bounds it is not the
    /// grant but a check that says which internals Lab may actually reach.
    /// <para>
    /// One is documented and allowed. `ServiceMetadata.TryGetCollectionElementType` is an internal MEMBER of a
    /// PUBLIC type, which is exactly the shape a type-level sweep cannot see — the type is public, so nothing about
    /// the reference looks unusual until the member is resolved. A reach into an internal TYPE is the simpler case
    /// the same resolution reports, and none is allowed.
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
                ("Ihc.ServiceMetadata", "TryGetCollectionElementType"),
            };

        [Test]
        public void Lab_ReachesOnlyItsDocumentedInternals()
        {
            var reaches = InternalReaches();

            Assert.That(reaches.Select(r => (r.TargetType, r.Member)).Distinct().Except(AllowedReaches), Is.Empty,
                "the ihc_lab friend grant is bounded to its documented dependencies: a new internal reach means "
                + "the SDK is missing a supported member, not that the grant should widen — offending calls: "
                + string.Join("; ", reaches
                    .Where(r => !AllowedReaches.Contains((r.TargetType, r.Member)))
                    .Select(r => $"{r.Origin}.{r.OriginMember} -> {r.TargetType}.{r.Member}")));
        }

        /// <summary>
        /// A constructor call is judged by the overload it RESOLVES to. A public service type always has a public
        /// constructor, so the member-name rule above can never see Lab building one through an internal overload,
        /// such as the transport constructor tests use to substitute a socket. A service built that way would carry
        /// its own transport, past the stub latch, which no supported constructor allows.
        /// </summary>
        [Test]
        public void Lab_CallsNoInternalSdkConstructor()
        {
            var sdkConstructions = SdkConstructorCalls(typeof(IhcLab.App).Assembly, LabRoot, SdkAssembly).ToList();
            Assert.That(sdkConstructions, Is.Not.Empty,
                "Lab builds SDK types, so a scan that sees no SDK constructor call is not resolving constructors");

            var offending = sdkConstructions.Where(call => IsGrantOnly(call.Called)).ToList();

            Assert.That(offending, Is.Empty,
                "the ihc_lab friend grant does not extend to non-public SDK constructors: build the type through a "
                + "public constructor — offending calls: "
                + string.Join("; ", offending.Select(call => $"{call.Site} -> {call.Called.DeclaringType}{call.Called}")));
        }

        /// <summary>
        /// Arming control for <see cref="Lab_CallsNoInternalSdkConstructor"/>: over this assembly's seeds, the scan
        /// reports the call to the internal transport overload and not the call to the public one on the same type,
        /// which proves it resolves the overload rather than the type or the name.
        /// </summary>
        [Test]
        public void TheConstructorScanResolvesTheOverload()
        {
            Assembly seeds = typeof(Ihc.Transport.Seeded.SeededTransportService).Assembly;
            var reported = SdkConstructorCalls(seeds, typeof(Ihc.Transport.Seeded.SeededTransportService).Namespace!, seeds)
                .Where(call => IsGrantOnly(call.Called))
                .Select(call => call.Called)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(reported, Does.Contain(typeof(Ihc.Transport.Seeded.SeededTransportService).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string), typeof(System.Net.Http.HttpClient)])),
                    "the seeded call to the internal transport overload must be reported");
                Assert.That(reported, Does.Not.Contain(typeof(Ihc.Transport.Seeded.SeededTransportService).GetConstructor(
                    [typeof(string)])),
                    "the seeded call to the public overload of the same type must not be");
            });
        }

        /// <summary>Every constructor of a <paramref name="target"/> type called from the authored code under
        /// <paramref name="callerRoot"/>, as the exact overload the call resolves to.</summary>
        private static IEnumerable<(ContainmentSite Site, ConstructorInfo Called)> SdkConstructorCalls(
            Assembly callers, string callerRoot, Assembly target) =>
            AuthoredMembers.Of(callers, callerRoot)
                .SelectMany(member => IlBody.CalledMethods(member)
                    .OfType<ConstructorInfo>()
                    .Where(called => called.DeclaringType?.Assembly == target)
                    .Select(called => (ContainmentSite.For(member), called)));

        /// <summary>True for a constructor only the friend grant makes callable: one on an internal type, or an
        /// internal or private overload. A protected one is reachable by any subclass, so it says nothing about the
        /// grant.</summary>
        private static bool IsGrantOnly(ConstructorInfo constructor) =>
            !constructor.DeclaringType!.IsVisible
            || !(constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly);

        /// <summary>
        /// The arming control. Every documented reach must be OBSERVED, which is what proves the scan resolves
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
        /// the one this grant exists for.
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
