using System;
using System.Collections.Generic;
using System.Linq;
using ArchUnitNET.Domain;
using NUnit.Framework;
using static Ihc.Tests.ArchRuleHelpers;

namespace Ihc.Tests
{
    /// <summary>
    /// The SDK opens a network connection only at its named transport sites. Controller traffic leaves through
    /// <c>Client.Post</c>, which is also where a stub endpoint is routed to its stub and where a stub process
    /// refuses a real endpoint. A second door would bypass both, and no runtime test would notice, because every
    /// test that would have to notice goes through the first door. The OTLP self-check is the one other client the
    /// SDK opens, and it probes a telemetry endpoint, never a controller.
    /// </summary>
    /// <remarks>
    /// The generated SOAP layer holds WCF proxies whose constructors take an address, so a reference to
    /// <c>System.ServiceModel</c> from hand-written SDK code would be the start of a door that bypasses
    /// <c>Client</c> without constructing anything this scan names. Hence the third rule.
    /// </remarks>
    [TestFixture]
    public class SdkTransportArchitectureTests
    {
        private const string SdkRoot = "Ihc";
        private const string GeneratedSoapRoot = "Ihc.Soap.";
        private const string ServiceModelRoot = "System.ServiceModel.";

        // Internal, and this suite is deliberately no friend of the SDK, so it is named rather than typeof'd. A
        // stale name cannot pass silently: the positive control requires the scan to report exactly this site.
        private const string ControllerTransport = SdkRoot + ".Client";
        private static readonly string TelemetryProbe = typeof(TelemetrySelfCheck).FullName!;

        // The client of a registration routed to a stub host in another process. Its sockets connect only to a
        // loopback address, and it is reached only through routing, which sits in Client.Post, or by the identity
        // check a registration makes before any service can use it.
        private const string LoopbackStubTransport = SdkRoot + ".Stubbing.ControllerStubLoopback";

        /// <summary>The types whose construction opens, or can open, a network connection.</summary>
        private static readonly HashSet<string> NetworkClientTypes =
        [
            "System.Net.Http.HttpClient",
            "System.Net.Http.HttpClientHandler",
            "System.Net.Http.SocketsHttpHandler",
            "System.Net.Http.HttpMessageInvoker",
            "System.Net.WebClient",
            "System.Net.Sockets.Socket",
            "System.Net.Sockets.TcpClient",
            "System.Net.Sockets.UdpClient",
            "System.Net.WebSockets.ClientWebSocket",
        ];

        /// <summary>
        /// The members of those types that put bytes on a connection, by the type that declares them. A call is
        /// matched on its declaring type, so an inherited send is listed on the type that declares it.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> SendMembers = new()
        {
            ["System.Net.Http.HttpClient"] =
            [
                "Send", "SendAsync", "GetAsync", "PostAsync", "PutAsync", "PatchAsync", "DeleteAsync",
                "GetStringAsync", "GetByteArrayAsync", "GetStreamAsync",
            ],
            ["System.Net.Http.HttpMessageInvoker"] = ["Send", "SendAsync"],
            ["System.Net.WebClient"] =
            [
                "DownloadData", "DownloadDataAsync", "DownloadDataTaskAsync", "DownloadFile", "DownloadFileAsync",
                "DownloadFileTaskAsync", "DownloadString", "DownloadStringAsync", "DownloadStringTaskAsync",
                "OpenRead", "OpenReadAsync", "OpenReadTaskAsync", "OpenWrite", "OpenWriteAsync", "OpenWriteTaskAsync",
                "UploadData", "UploadDataAsync", "UploadDataTaskAsync", "UploadFile", "UploadFileAsync",
                "UploadFileTaskAsync", "UploadString", "UploadStringAsync", "UploadStringTaskAsync",
                "UploadValues", "UploadValuesAsync", "UploadValuesTaskAsync",
            ],
            ["System.Net.Sockets.Socket"] =
            [
                "Connect", "ConnectAsync", "BeginConnect", "Send", "SendAsync", "SendTo", "SendToAsync",
                "BeginSend", "BeginSendTo", "SendFile", "SendFileAsync", "SendPackets", "SendPacketsAsync",
            ],
            ["System.Net.Sockets.TcpClient"] = ["Connect", "ConnectAsync", "BeginConnect"],
            ["System.Net.Sockets.UdpClient"] = ["Connect", "Send", "SendAsync", "BeginSend"],
            ["System.Net.WebSockets.ClientWebSocket"] = ["ConnectAsync", "SendAsync"],
        };

        private static readonly HashSet<string> ConstructionAllowedIn = [ControllerTransport, TelemetryProbe, LoopbackStubTransport];

        private static readonly HashSet<MethodCallExemption> SendAllowedFrom =
        [
            new(ControllerTransport, "Post"),
            new(TelemetryProbe, nameof(TelemetrySelfCheck.ProbeAsync)),
            // The identity check, sent before the registration exists, so it cannot go through routing.
            new(LoopbackStubTransport, "PreflightAsync"),
            // The connect gate, which opens the loopback socket a request through the client above travels on.
            new(LoopbackStubTransport, "ConnectLoopbackOnlyAsync"),
        ];

        [Test]
        public void TheSdkConstructsNetworkClientsOnlyAtItsTransportSites()
        {
            var offending = NetworkClientConstructions(ArchitectureModels.Sdk, SdkRoot)
                .Where(site => !ConstructionAllowedIn.Contains(site.Site))
                .ToList();

            Assert.That(offending, Is.Empty,
                "a network client built outside the SDK's transport sites is a door to a controller that neither "
                + "stub routing nor the stub latch guards; send through Client instead — offenders: "
                + string.Join("; ", offending.Select(site => $"{site.Site} -> {site.TargetType}")));
        }

        [Test]
        public void TheSdkSendsOnlyFromItsTransportSites()
        {
            var offending = NetworkSends(ArchitectureModels.Sdk, SdkRoot)
                .Where(site => !SendAllowedFrom.Contains(site))
                .ToList();

            Assert.That(offending, Is.Empty,
                "a send outside Client.Post bypasses stub routing and the stub latch, which both sit there — "
                + "offenders: " + string.Join("; ", offending.Select(site => $"{site.OriginTypeFullName}.{site.OriginMemberName}")));
        }

        /// <summary>
        /// Positive control for both rules above: with nothing allowed, the scans report exactly the allowed
        /// sites. That proves they see the real transport, including the construction and the send written inside
        /// async bodies, and that no allowance names a site the scan cannot find.
        /// </summary>
        [Test]
        public void TransportScans_WithTheAllowancesRemoved_ReportExactlyTheAllowedSites()
        {
            Assert.Multiple(() =>
            {
                Assert.That(NetworkClientConstructions(ArchitectureModels.Sdk, SdkRoot).Select(site => site.Site).Distinct(),
                    Is.EquivalentTo(ConstructionAllowedIn), "construction sites");
                Assert.That(NetworkSends(ArchitectureModels.Sdk, SdkRoot),
                    Is.EquivalentTo(SendAllowedFrom), "send sites");
            });
        }

        [Test]
        public void NothingOutsideTheGeneratedSoapLayerReferencesServiceModel()
        {
            Assert.That(DependencyEdges(ArchitectureModels.Sdk, SdkRoot)
                    .Any(edge => edge.Origin.StartsWith(GeneratedSoapRoot, StringComparison.Ordinal)
                                 && edge.Target.StartsWith(ServiceModelRoot, StringComparison.Ordinal)),
                Is.True,
                "the generated layer is built on System.ServiceModel, so a scan that sees no such reference there "
                + "is seeing nothing, and its verdict below would be meaningless");

            var offending = ServiceModelReferencesOutsideSoap(ArchitectureModels.Sdk, SdkRoot).ToList();

            Assert.That(offending, Is.Empty,
                "hand-written SDK code may not reach System.ServiceModel: a WCF proxy takes an address and sends "
                + "on its own, past stub routing and the stub latch — offenders: "
                + string.Join("; ", offending.Select(edge => $"{edge.Origin} -> {edge.Target}")));
        }

        /// <summary>
        /// Seeded control for the rule above, whose production verdict is empty: the same scan over this assembly
        /// must report the seeded reference, proving an empty result means "none" rather than "not seen".
        /// </summary>
        [Test]
        public void ServiceModelScan_IsArmed() =>
            Assert.That(
                ServiceModelReferencesOutsideSoap(ArchitectureModels.ArchitectureTests.Value, SdkRoot)
                    .Select(edge => OutermostTypeName(edge.Origin)),
                Does.Contain(typeof(Ihc.Transport.Seeded.SeededServiceModelReference).FullName),
                "the scan must report the seeded System.ServiceModel reference outside the generated layer");

        private static IEnumerable<(string Site, string TargetType)> NetworkClientConstructions(
            Architecture arch, string root) =>
            ConstructorCallEdgesWithOrigin(arch, root)
                .Where(edge => NetworkClientTypes.Contains(edge.TargetType))
                .Select(edge => (Site: OutermostTypeName(edge.Origin), edge.TargetType))
                .Distinct();

        private static IEnumerable<MethodCallExemption> NetworkSends(Architecture arch, string root) =>
            MethodCallEdges(arch, root)
                .Where(edge => SendMembers.TryGetValue(edge.TargetType, out HashSet<string>? sends)
                               && sends.Contains(edge.Member))
                .Select(edge => new MethodCallExemption(OutermostTypeName(edge.Origin), edge.OriginMember))
                .Distinct();

        private static IEnumerable<(string Origin, string Target)> ServiceModelReferencesOutsideSoap(
            Architecture arch, string root) =>
            DependencyEdges(arch, root)
                .Where(edge => edge.Target.StartsWith(ServiceModelRoot, StringComparison.Ordinal)
                               && !edge.Origin.StartsWith(GeneratedSoapRoot, StringComparison.Ordinal));
    }
}
