using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// An executable the switch is wired into opens no network connection of its own. Routing, the latch and the switch all act in one
    /// place, <c>Client.Post</c>, so a connection an executable opened itself would reach whatever it named with
    /// none of the three in the way: in a process started with the switch, in a suite that composes the executable
    /// in-process under the latch, or in a manual run that believes it is stubbed. The SDK confines its own network
    /// clients to named sites; this is the executables' half of the same rule (L12), held to the controller stub's
    /// standard, because an executable has no more reason to connect anywhere itself than the stub has.
    /// </summary>
    /// <remarks>
    /// The population is the switch's roster (every wired executable, and the bootstrap library that prints their
    /// console stub line) plus the Avalonia bootstrap the two desktop applications share. A client a referenced
    /// library builds inside itself, such as the OTLP exporter's, is out of scope: it is that library's code, not
    /// the executable's. The SDK examples and the settings encryptor are not wired, so they never run switched or
    /// composed in a stub suite. Nothing is admitted.
    /// </remarks>
    [TestFixture]
    public class ExecutableOutboundArchitectureTests
    {
        private static IEnumerable<string> Population() =>
            StubSurfaceArchitectureTests.Executables.Select(executable => executable.Assembly).Append("ihc_hostbootstrap_avalonia");

        [Test]
        public void NoExecutableOpensAConnectionOfItsOwn()
        {
            var assemblies = Population().Select(name => Assembly.Load(new AssemblyName(name))).ToList();

            Assert.That(assemblies.Where(assembly => !AuthoredMembers.Of(assembly).Any()).Select(assembly => assembly.GetName().Name),
                Is.Empty, "the scan must see each assembly's members, or it judges nothing");

            var offending = assemblies
                .SelectMany(assembly => OutboundConnectionScan.Reaches(assembly)
                    .Select(reach => $"{assembly.GetName().Name}: {reach.Site} -> {reach.Target}"))
                .Distinct()
                .ToList();

            Assert.That(offending, Is.Empty,
                "an executable sends controller traffic only through the SDK, where routing, the latch and the switch "
                + "act; a client, handler, socket or WCF channel of its own bypasses all three — offenders: "
                + string.Join("; ", offending));
        }

        /// <summary>
        /// Positive control: the same scan over this assembly reports the seeded client built over a handler this
        /// assembly declares, a shape the controller-reach guard deliberately lets a test suite build. The
        /// executables' production verdict is empty, so only a seed shows the scan sees that shape at all.
        /// </summary>
        [Test]
        public void TheOutboundScan_ReportsAClientOverADeclaredHandler()
        {
            var reported = OutboundConnectionScan.Reaches(typeof(ExecutableOutboundArchitectureTests).Assembly)
                .Where(reach => reach.Site.Type == typeof(Ihc.Safety.Seeded.SeededClientHandlerSubclassUser).FullName)
                .Select(reach => reach.Target)
                .ToList();

            Assert.That(reported, Does.Contain(ControllerReachScan.HttpClientFullName),
                "a client is outbound whatever handler it is built over");
        }
    }
}
