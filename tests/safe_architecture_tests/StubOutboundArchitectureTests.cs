using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller stub opens no outbound connection. It answers in place of a controller, so a connection it
    /// opened could only go to something else: a real controller behind a test that believes it is stubbed, or the
    /// network a manual run sits on. The SDK guarantees that no SDK socket serves a stub endpoint; this is the
    /// stub's half of that guarantee.
    /// </summary>
    /// <remarks>
    /// The loopback host listens through Kestrel, which runs in framework code, so the stub's own IL needs none of
    /// the client-side types refused here. A listener-side member it does need is admitted by name with its reason.
    /// What counts as outbound is <see cref="OutboundConnectionScan"/>, which the executables are held to as well.
    /// </remarks>
    [TestFixture]
    public class StubOutboundArchitectureTests
    {
        private static readonly Assembly StubAssembly = Assembly.Load(new AssemblyName("ihcclient_stub"));

        /// <summary>Listener-side sites the stub may use although the scan would refuse them, each with its
        /// reason. Keyed by site.</summary>
        private static readonly Dictionary<string, string> Admitted = [];

        [Test]
        public void TheStubOpensNoOutboundConnection()
        {
            Assert.That(AuthoredMembers.Of(StubAssembly), Is.Not.Empty,
                "the scan must see the stub's members, or it judges nothing");

            var offending = OutboundConnectionScan.Reaches(StubAssembly)
                .Where(reach => !Admitted.ContainsKey(reach.Site.ToString()))
                .ToList();

            Assert.That(offending, Is.Empty,
                "the controller stub answers in place of a controller and may connect to nothing; a listener-side "
                + "member it needs is admitted by name with its reason — offenders: "
                + string.Join("; ", offending.Select(reach => $"{reach.Site} -> {reach.Target}")));
        }

        /// <summary>
        /// Positive control: the same scan over this assembly reports the seeded <c>new HttpClient()</c>. The
        /// stub's production verdict is empty, so only a seed shows an empty result means "none" rather than
        /// "not seen".
        /// </summary>
        [Test]
        public void TheOutboundScan_IsArmed()
        {
            var reported = OutboundConnectionScan.Reaches(typeof(StubOutboundArchitectureTests).Assembly)
                .Where(reach => reach.Site.Type == typeof(Ihc.Transport.Seeded.SeededOutboundClient).FullName)
                .Select(reach => reach.Target)
                .ToList();

            Assert.That(reported, Does.Contain(ControllerReachScan.HttpClientFullName),
                "the scan must report the seeded parameterless HttpClient construction");
        }
    }
}
