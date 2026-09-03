using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The armed controls for the safety rule's guard. The guard itself is
    /// <see cref="ControllerReachGuard"/>, compiled into each controller-free suite so it can scan the assembly
    /// it runs in; this fixture proves the same scan can FAIL, which that guard cannot demonstrate about itself.
    ///
    /// <para>The seeds live in this assembly for the reason the guard is not compiled here: a violator planted in
    /// a suite the guard polices would fail the very rule it exists to arm.</para>
    /// </summary>
    [TestFixture]
    public class ControllerReachArchitectureTests
    {
        private static readonly ControllerReachScan.Anchors Anchors =
            new("Ihc.IIHCApiService", "Ihc.Soap.", new System.Collections.Generic.Dictionary<string, string>());

        private static System.Collections.Generic.IReadOnlyList<string> SeededSites() =>
            ControllerReachScan.Sites(typeof(ControllerReachArchitectureTests).Assembly, Anchors);

        /// <summary>
        /// The detector can fail. Without this the guard's verdict in four clean suites is indistinguishable
        /// from a scan that reads nothing.
        /// </summary>
        [Test]
        public void TheSeededReacher_IsFlagged() =>
            Assert.That(SeededSites(),
                Does.Contain("Ihc.Safety.Seeded.SeededControllerReacher.Build"),
                "a service built through the constructor that makes its own transport is exactly what the rule "
                + "forbids a controller-free suite from holding");

        /// <summary>
        /// A double the suite declares itself is not a controller service. Flagging it would indict every test
        /// that writes its own stub, which is the shape the safety rule PRESCRIBES rather than forbids.
        /// </summary>
        [Test]
        public void TheSeededLocalStub_IsNotFlagged() =>
            Assert.That(SeededSites(),
                Does.Not.Contain("Ihc.Safety.Seeded.SeededLocalStubUser.Build"),
                "constructing a stub this assembly declares reaches no network");

        /// <summary>
        /// The scan's SECOND exclusion — a constructor HANDED its transport — was the one with no control behind
        /// it, and it is load-bearing: a shipped suite depends on not being flagged for using that overload with
        /// a fake. These two pin it by its ANCHOR rather than by a seed, because it cannot be seeded here.
        /// </summary>
        /// <remarks>
        /// A seed would have to CALL such a constructor, and all of them are <c>internal</c> to the SDK, which
        /// does not grant this assembly access — granting it, for a test control, would widen the SDK's internal
        /// surface to buy one seed. What the seed would have caught is a widened prefix silently excluding the
        /// dangerous constructors too, and that is exactly what the second assertion states.
        /// </remarks>
        [Test]
        public void TheTransportExclusion_StillDescribesARealConstructor()
        {
            var handed = typeof(IIHCApiService).Assembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.FullName == Anchors.MarkerInterfaceFullName))
                .SelectMany(t => t.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                .Where(c => c.GetParameters().Any(p => p.ParameterType.FullName?
                    .StartsWith(Anchors.TransportNamespace, System.StringComparison.Ordinal) == true))
                .ToList();

            Assert.That(handed, Is.Not.Empty,
                "the exclusion describes an overload that no longer exists — an exclusion matching nothing is "
                + "how a rule quietly stops meaning what it says");
        }

        /// <summary>
        /// The disarm this exclusion is one edit away from: shorten the prefix by a segment and EVERY service
        /// constructor is excluded, because they all take a type under <c>Ihc.</c> — and four suites go on
        /// reporting green over a rule that now matches nothing.
        /// </summary>
        [Test]
        public void TheTransportPrefix_DoesNotReachTheTypesTheDangerousConstructorsTake() =>
            Assert.Multiple(() =>
            {
                Assert.That(typeof(IhcSettings).FullName,
                    Does.Not.StartWith(Anchors.TransportNamespace),
                    "a settings-built service makes its OWN transport and must stay flagged");
                Assert.That(typeof(IAuthenticationService).FullName,
                    Does.Not.StartWith(Anchors.TransportNamespace),
                    "and so must one built from an authentication it shares a session with");
            });

        /// <summary>
        /// The scan's KNOWN LIMIT, asserted so it is a fact with a name. A service built by a product-side
        /// factory is not seen, because the <c>newobj</c> is in the SDK and not in the suite.
        /// </summary>
        /// <remarks>
        /// Read this as a measurement, never as a permission: the shape is not safe by construction the way the
        /// two exclusions above are, and what actually holds these paths is the seam — an endpoint that resolves
        /// nowhere and no operation called. Closing it means deriving the factory set from the product assembly,
        /// at which point this assertion flips and the diff says so.
        /// </remarks>
        [Test]
        public void TheSeededFactoryReacher_IsNotFlagged_WhichIsTheScansOneBlindSpot() =>
            Assert.That(SeededSites(),
                Does.Not.Contain("Ihc.Safety.Seeded.SeededFactoryReacher.Build"),
                "if this now FAILS the scan has learned to follow factories — good; move the guard's "
                + "documented limit and flip this control rather than deleting it");

        /// <summary>
        /// The scan reads the marker interface rather than a name pattern, so a service that stops carrying it
        /// stops being policed. This pins that the interface still names something.
        /// </summary>
        [Test]
        public void TheMarkerInterface_StillExists() =>
            Assert.That(typeof(IIHCApiService).FullName, Is.EqualTo("Ihc.IIHCApiService"),
                "the guard is anchored on this name in every suite; renaming it would silently disarm them all");
    }
}
