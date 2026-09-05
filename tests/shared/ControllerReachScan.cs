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
        /// The HTTP client, which is both a door a caller hands a transport through and — built with no
        /// handler — a live socket of its own. Spelled once here so an anchor list and the live-transport
        /// rule cannot come to mean different types.
        /// </summary>
        internal const string HttpClientFullName = "System.Net.Http.HttpClient";

        /// <summary>The interface every controller-facing service carries, so it is what "reaches a
        /// controller" means.</summary>
        internal const string MarkerInterfaceFullName = "Ihc.IIHCApiService";

        /// <summary>
        /// One door a caller hands a service its transport through, WITH the rule for matching a type
        /// against it. The rule travels with the door because the two doors need different ones: a whole
        /// generated namespace is matched by prefix, while <c>HttpClient</c> is one exact type — matched by
        /// prefix it would also admit <c>HttpClientHandler</c>, exempting a service handed one while
        /// <see cref="IsLiveTransport"/>, which compares the name exactly, would not count the same type as
        /// a door at all.
        /// </summary>
        internal sealed record TransportDoor(string TypeName, bool WholeNamespace)
        {
            internal bool Admits(Type parameterType) =>
                parameterType.FullName is { } name
                && (WholeNamespace ? name.StartsWith(TypeName, StringComparison.Ordinal) : name == TypeName);

            public override string ToString() => WholeNamespace ? TypeName + "*" : TypeName;
        }

        /// <summary>
        /// The doors, declared once for every suite and for the architecture suite that proves them. They sit
        /// at different heights: the generated SOAP layer replaces the whole message exchange, and
        /// <c>HttpClient</c> replaces only the socket underneath it, leaving the real serializer, headers and
        /// cookie handling in place. A service is only reachable through the door its caller opened either
        /// way — which makes both exclusions safe only alongside <see cref="IsLiveTransport"/>, the rule that
        /// stops a suite opening a REAL door and handing that through instead.
        /// </summary>
        internal static readonly IReadOnlyList<TransportDoor> TransportDoors =
        [
            new("Ihc.Soap.", WholeNamespace: true),
            new(HttpClientFullName, WholeNamespace: false),
        ];

        /// <summary>
        /// What counts as a controller service, and who may still build one.
        /// </summary>
        /// <param name="MarkerInterfaceFullName">The interface every controller-facing service carries.</param>
        /// <param name="Admitted">Sites allowed to construct one anyway, each with its reason.</param>
        /// <param name="TransportDoors">The doors a caller hands a service its transport through.</param>
        internal sealed record Anchors(
            string MarkerInterfaceFullName, IReadOnlyList<TransportDoor> TransportDoors,
            IReadOnlyDictionary<string, string> Admitted);

        /// <summary>The anchors every suite scans by: the marker interface and the doors above, with only the
        /// admissions differing. Declared here so a suite and the architecture suite that proves its exclusions
        /// cannot come to scan by different ones.</summary>
        internal static Anchors AnchorsFor(IReadOnlyDictionary<string, string> admitted) =>
            new(MarkerInterfaceFullName, TransportDoors, admitted);

        /// <summary>Every site in <paramref name="assembly"/> that can reach a controller — one that constructs
        /// a service which makes its OWN transport, and one that constructs a LIVE transport such a service
        /// could be handed — named the way a reader finds it: the outermost type and the method it was written
        /// in.</summary>
        internal static IReadOnlyList<string> Sites(Assembly assembly, Anchors anchors) =>
            [.. AuthoredMembers.Of(assembly)
                .Where(m => CanReachAController(m, anchors))
                .Select(Name)
                .Distinct()
                .OrderBy(site => site, StringComparer.Ordinal)];

        /// <summary>
        /// Both halves of the rule, asked of ONE walk of the body. <see cref="IlBody.Instructions"/> re-reads
        /// and re-decodes the whole method on each enumeration, so asking them separately decoded every method
        /// in the assembly twice over — the disjunction short-circuits only for the rare member that is already
        /// a hit.
        /// </summary>
        private static bool CanReachAController(MethodBase method, Anchors anchors)
        {
            Assembly? scanned = method.DeclaringType?.Assembly;
            return IlBody.Instructions(method)
                .Where(instruction => instruction.Op == OpCodes.Newobj)
                .Select(instruction => instruction.Called)
                .OfType<MethodBase>()
                .Any(ctor => ReachesAController(ctor, scanned, anchors) || IsLiveTransport(ctor, scanned));
        }

        /// <summary>
        /// True for a constructor that yields a service able to talk to a wire. Two exclusions, and both are
        /// the difference between a rule that describes the hazard and one that describes every test:
        /// </summary>
        /// <remarks>
        /// <para><b>A type the suite declares itself is not a controller service.</b> A test-local stub
        /// implementing the marker interface answers from a field; it has no endpoint and no transport.</para>
        /// <para><b>A constructor handed its transport is not reaching either.</b> Those overloads exist so a
        /// test can supply the way onto the wire, so such a service reaches exactly as far as the transport its
        /// caller built — and building a LIVE one is itself flagged, by
        /// <see cref="IsLiveTransport"/>. That second half is what makes this exclusion exact rather
        /// than a claim about what the call sites happen to pass today. What reaches a controller unconditionally
        /// is the overload that builds its own client from settings, because nothing in the test then stands
        /// between the service and the network.</para>
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
            return !ctor.GetParameters().Any(p => IsTransport(p.ParameterType, anchors));
        }

        /// <summary>True for a parameter type through which a caller hands a service its transport.</summary>
        internal static bool IsTransport(Type parameterType, Anchors anchors) =>
            anchors.TransportDoors.Any(door => door.Admits(parameterType));

        /// <summary>
        /// True for a constructor that yields a transport reaching a real network — the other half of the
        /// transport exclusion above, and what makes it safe rather than merely conventional.
        /// </summary>
        /// <remarks>
        /// A service handed its transport reaches only as far as that transport does, so the exclusion is exact
        /// PROVIDED the suite cannot produce a live one. It can, in three shapes, and each is a <c>newobj</c>
        /// the walk above sees: <c>new HttpClient()</c>, whose socket is the BCL's own; an
        /// <c>HttpMessageHandler</c> the suite did not declare itself, which is the chain such a client is built
        /// over; and a generated <c>Ihc.Soap.*Client</c>, which derives from <c>ClientBase&lt;T&gt;</c> and
        /// speaks the whole SOAP exchange over a binding of its own.
        /// <para>Judged where the transport is CONSTRUCTED rather than where it is handed over. Following the
        /// value to its call site needs dataflow the rest of this scan does without, and construction is the
        /// same standard the service half is held to — a suite with no reason to build a live transport has no
        /// reason to build one it never passes anywhere either.</para>
        /// </remarks>
        private static bool IsLiveTransport(MethodBase ctor, Assembly? scanned)
        {
            Type? built = ctor.DeclaringType;
            if (built is null || built.Assembly == scanned)
            {
                // A handler the suite declares is a stub, for the reason a service it declares is one: it
                // answers from its own fields, with no socket underneath.
                return false;
            }
            if (built.FullName == HttpClientFullName)
            {
                // Handed a handler, a client reaches only where that handler does — and a live handler is
                // caught below. Handed NONE, the handler is the BCL's own, substituted by nobody.
                return ctor.GetParameters().Length == 0;
            }
            return DerivesFrom(built, "System.Net.Http.HttpMessageHandler")
                || DerivesFrom(built, "System.ServiceModel.ClientBase`1");
        }

        /// <summary>Walks the base chain by NAME, matching a generic base by its definition.</summary>
        private static bool DerivesFrom(Type type, string baseTypeFullName)
        {
            for (Type? at = type; at is not null; at = at.BaseType)
            {
                string? name = at.IsGenericType ? at.GetGenericTypeDefinition().FullName : at.FullName;
                if (name == baseTypeFullName)
                {
                    return true;
                }
            }
            return false;
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
