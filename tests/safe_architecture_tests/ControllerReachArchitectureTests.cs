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
        // The same anchors every guarded suite scans by, admitting nothing: these controls are about what the
        // scan SEES, and a suite-specific admission would hide a seed rather than test one.
        private static readonly ControllerReachScan.Anchors Anchors =
            ControllerReachScan.AnchorsFor(new System.Collections.Generic.Dictionary<string, string>());

        // One scan for the whole fixture: it is a pure function of this assembly and the anchors above, and
        // every case below asks the same question of it.
        private static readonly System.Lazy<System.Collections.Generic.IReadOnlyList<string>> Seeded =
            new(() => ControllerReachScan.Sites(typeof(ControllerReachArchitectureTests).Assembly, Anchors));

        private static System.Collections.Generic.IReadOnlyList<string> SeededSites() => Seeded.Value;

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
        /// The live transport a suite could hand through an exempt constructor. The exclusion below says such a
        /// service reaches only as far as its caller's transport; this is the control for the rule that keeps
        /// that true, by refusing to let the caller build a real one.
        /// </summary>
        [Test]
        public void TheSeededLiveTransport_IsFlagged() =>
            Assert.That(SeededSites(),
                Does.Contain("Ihc.Safety.Seeded.SeededLiveTransportBuilder.Build"),
                "an HttpClient given no handler is the BCL's own socket - handed to a service through the "
                + "transport overload, it reaches whatever address the settings name");

        /// <summary>
        /// And its negative: a client built over a handler the suite declares. Flagging this would indict the
        /// substitution the safety rule PRESCRIBES, exactly as flagging a locally declared stub service would.
        /// </summary>
        [Test]
        public void TheSeededStubTransport_IsNotFlagged() =>
            Assert.That(SeededSites(),
                Does.Not.Contain("Ihc.Safety.Seeded.SeededStubTransportUser.Build"),
                "a client over a handler this assembly declares reaches no network");

        /// <summary>
        /// The scan's SECOND exclusion — a constructor HANDED its transport — is load-bearing: a shipped suite
        /// depends on not being flagged for using such an overload with a fake. The two controls above pin the
        /// half that decides whether the transport is live; these two pin the other half by its ANCHOR, per
        /// DOOR, so a door that stops existing is named rather than covered for by the other.
        /// </summary>
        /// <remarks>
        /// This half cannot be seeded: a seed would have to CALL such a constructor, and all of them are
        /// <c>internal</c> to the SDK, which does not grant this assembly access — granting it, for a test
        /// control, would widen the SDK's internal surface to buy one seed. What the seed would have caught is
        /// a widened door silently excluding the dangerous constructors too, and that is exactly what the
        /// second assertion states. The door's OWN matcher is used rather than a copy of it, so a control that
        /// passes is a statement about the rule that runs.
        /// </remarks>
        [TestCaseSource(nameof(TransportDoorNames))]
        public void EachTransportExclusion_StillDescribesARealConstructor(string doorName)
        {
            // Named rather than passed: the door type is internal to the scan, and an NUnit case parameter
            // travels through a public signature.
            ControllerReachScan.TransportDoor door =
                ControllerReachScan.TransportDoors.Single(d => d.ToString() == doorName);

            var handed = typeof(IIHCApiService).Assembly.GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.FullName == Anchors.MarkerInterfaceFullName))
                .SelectMany(t => t.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                .Where(c => c.GetParameters().Any(p => door.Admits(p.ParameterType)))
                .ToList();

            Assert.That(handed, Is.Not.Empty,
                $"the exclusion for '{door}' describes an overload that no longer exists — an exclusion "
                + "matching nothing is how a rule quietly stops meaning what it says");
        }

        private static System.Collections.Generic.IEnumerable<string> TransportDoorNames() =>
            ControllerReachScan.TransportDoors.Select(d => d.ToString());

        /// <summary>
        /// The disarm this exclusion is one edit away from: widen a door by a segment and EVERY service
        /// constructor is excluded, because they all take a type under <c>Ihc.</c> — and four suites go on
        /// reporting green over a rule that now matches nothing. <c>HttpClientHandler</c> is the near miss on
        /// the other door: it shares its whole name with <c>HttpClient</c> as a prefix, so a door matching by
        /// prefix would exempt a service handed one while the live-transport rule, which compares exactly,
        /// would not count it as a door at all.
        /// </summary>
        [Test]
        public void TheTransportDoors_DoNotReachTheTypesTheDangerousConstructorsTake() =>
            Assert.Multiple(() =>
            {
                Assert.That(ControllerReachScan.IsTransport(typeof(IhcSettings), Anchors), Is.False,
                    "a settings-built service makes its OWN transport and must stay flagged");
                Assert.That(ControllerReachScan.IsTransport(typeof(IAuthenticationService), Anchors), Is.False,
                    "and so must one built from an authentication it shares a session with");
                Assert.That(ControllerReachScan.IsTransport(typeof(System.Net.Http.HttpClientHandler), Anchors),
                    Is.False,
                    "the HttpClient door is one exact type, not everything whose name starts with it");
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
