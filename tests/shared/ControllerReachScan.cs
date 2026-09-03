using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Ihc.Tests
{
    /// <summary>
    /// Finds every place a test assembly CONSTRUCTS a real controller service.
    ///
    /// <para><b>Why construction, and not the call.</b> A controller-free suite reaches
    /// <c>IAuthenticationService</c> constantly — on FakeItEasy proxies, which is the seam the safety rule
    /// prescribes. Those calls are indistinguishable in IL from calls to a real one, because both go through the
    /// interface. What is not ambiguous is who built the receiver: a fake is constructed by the framework through
    /// reflection and emits no <c>newobj</c> here, while a real service arrives from a <c>newobj</c>. So the rule
    /// is about the constructor, and where it applies it is exact rather than approximate.</para>
    ///
    /// <para><b>What it therefore does NOT see: a service built for the test by a product-side factory.</b>
    /// <c>ProjectAppService.CreateWithControllerBridge</c> is the live example — the <c>newobj</c> is in the SDK,
    /// so a test calling it holds a real service that this scan never names. Following one call level would catch
    /// that particular shape and not the next one down, which is an arbitrary depth dressed up as a rule; the
    /// honest statement is that this detects DIRECT construction, and the limit is pinned by a negative control
    /// rather than left for a reader to discover. What still holds the factory paths is the seam itself: the
    /// endpoint a controller-free suite configures resolves nowhere, and no operation is called on the result.
    /// Closing the gap properly means deriving the factory set from the product assembly — every method whose own
    /// IL constructs a controller service — and treating a call to one as construction. That is a real detector,
    /// not a patch, and it belongs in its own measured change.</para>
    ///
    /// <para><b>Why it runs inside each suite.</b> A rule about what a test assembly does has to be compiled into
    /// that assembly: the architecture suite references the product, not its siblings, and cannot scan an
    /// assembly it has never heard of. Hence a shared fixture the controller-free suites host, in the shape
    /// <c>NoLeakedHarness</c> already uses for the same reason.</para>
    /// </summary>
    internal static class ControllerReachScan
    {
        /// <summary>
        /// What counts as a controller service, and who may still build one.
        /// </summary>
        /// <param name="MarkerInterfaceFullName">The interface every controller-facing service carries.</param>
        /// <param name="Admitted">Sites allowed to construct one anyway, each with its reason.</param>
        /// <param name="TransportNamespace">The generated SOAP layer. A constructor taking one of its types is
        /// being handed its transport by the caller, which is how a test supplies a fake one.</param>
        internal sealed record Anchors(
            string MarkerInterfaceFullName, string TransportNamespace, IReadOnlyDictionary<string, string> Admitted);

        /// <summary>Every site in <paramref name="assembly"/> that constructs a controller service, named the
        /// way a reader finds it: the outermost type and the method it was written in.</summary>
        internal static IReadOnlyList<string> Sites(Assembly assembly, Anchors anchors) =>
            [.. AuthoredMembers.Of(assembly)
                .Where(m => ConstructsAControllerService(m, anchors))
                .Select(Name)
                .Distinct()
                .OrderBy(site => site, StringComparer.Ordinal)];

        internal static bool ConstructsAControllerService(MethodBase method, Anchors anchors) =>
            IlBody.Instructions(method)
                .Where(instruction => instruction.Op == OpCodes.Newobj)
                .Select(instruction => instruction.Called)
                .OfType<MethodBase>()
                .Any(ctor => ReachesAController(ctor, method.DeclaringType?.Assembly, anchors));

        /// <summary>
        /// True for a constructor that yields a service able to talk to a wire. Two exclusions, and both are
        /// the difference between a rule that describes the hazard and one that describes every test:
        /// </summary>
        /// <remarks>
        /// <para><b>A type the suite declares itself is not a controller service.</b> A test-local stub
        /// implementing the marker interface answers from a field; it has no endpoint and no transport.</para>
        /// <para><b>A constructor handed a SOAP client is not reaching either.</b> That overload exists so a
        /// test can supply the transport, and every such call site here passes a FAKE one. What reaches a
        /// controller is the overload that builds its own client from settings, because nothing in the test
        /// then stands between the service and the network.</para>
        /// </remarks>
        private static bool ReachesAController(MethodBase ctor, Assembly? scanned, Anchors anchors)
        {
            Type? built = ctor.DeclaringType;
            if (built is null || built.IsInterface || built.Assembly == scanned)
            {
                return false;
            }
            if (!built.GetInterfaces().Any(i => i.FullName == anchors.MarkerInterfaceFullName))
            {
                return false;
            }
            return !ctor.GetParameters().Any(p =>
                p.ParameterType.FullName?.StartsWith(anchors.TransportNamespace, StringComparison.Ordinal) == true);
        }

        /// <summary>The authored site, with a compiler carrier resolved back to the method it came from.</summary>
        private static string Name(MethodBase method)
        {
            Type declaring = method.DeclaringType!;
            string type = declaring.FullName!;
            int nested = type.IndexOf('+');
            if (nested > 0)
            {
                type = type[..nested];
            }
            string member = method.Name;
            if (member.StartsWith('<'))
            {
                int close = member.IndexOf('>', 1);
                member = close > 1 ? member[1..close] : member;
            }
            else if (declaring.Name.StartsWith('<'))
            {
                int start = declaring.Name.StartsWith("<<", StringComparison.Ordinal) ? 2 : 1;
                int close = declaring.Name.IndexOf('>', start);
                member = close > start ? declaring.Name[start..close] : member;
            }
            return $"{type}.{member}";
        }
    }
}
