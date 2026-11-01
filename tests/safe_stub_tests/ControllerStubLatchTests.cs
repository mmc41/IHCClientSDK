using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Envelope;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using NUnit.Framework;
using Auth = Ihc.Soap.Authentication;

namespace Ihc.Tests
{
    /// <summary>
    /// The latch: while any controller stub is registered, the SDK refuses every endpoint that would reach its
    /// shared socket client, so a service built by hand from real settings during a stub run cannot reach a
    /// controller. Its release is read from the registry rather than by sending a call, because a released latch
    /// would let a probe's call go to the network.
    /// </summary>
    /// <remarks>
    /// Every probe targets a host under the reserved <c>.invalid</c> name and carries no credential, so even a
    /// regressed latch ends in a transport failure for a name that never resolves, never at a device.
    /// Non-parallelizable because the latch is process-wide.
    /// </remarks>
    [TestFixture]
    [NonParallelizable]
    public class ControllerStubLatchTests
    {
        private const string LatchProbeEndpoint = "http://latch-probe.invalid";

        [Test]
        public void WhileAStubIsRegistered_ARealEndpointIsRefused_AndDisposingReleasesTheLatch()
        {
            var registration = ControllerStubs.Register(new FailingStub());
            try
            {
                AssertLatched(ProbeOn(LatchProbeEndpoint));
            }
            finally
            {
                registration.Dispose();
            }

            Assert.That(ControllerStubRegistry.AnyLive, Is.False, "disposing the only registration released the latch");
        }

        /// <summary>
        /// The registry is a set, so a second <c>Dispose</c> of one registration cannot release another's latch.
        /// </summary>
        [Test]
        public void DisposingOneRegistrationTwice_LeavesTheLatchHeldByAnother()
        {
            var first = ControllerStubs.Register(new FailingStub());
            var second = ControllerStubs.Register(new FailingStub());
            try
            {
                first.Dispose();
                first.Dispose();
                AssertLatched(ProbeOn(LatchProbeEndpoint));
            }
            finally
            {
                second.Dispose();
            }

            Assert.That(ControllerStubRegistry.AnyLive, Is.False, "disposing the other registration released it");
        }

        /// <summary>
        /// An explicit transport is a test's own and comes before the latch, so a suite that holds a stub for its
        /// whole run keeps its transports.
        /// </summary>
        [Test]
        public async Task WhileAStubIsRegistered_AnExplicitTransportStillCarriesItsCalls()
        {
            using var registration = ControllerStubs.Register(new FailingStub());
            var socket = new StubTransport(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Serialization.SerializeXml(new ResponseEnvelope<Auth.outputMessageName3>(
                    new Auth.outputMessageName3(true))), Encoding.UTF8, "text/xml"),
            });
            using var transport = Client.CreateHttpClient(socket);
            using var auth = new AuthenticationService(new IhcSettings { Endpoint = LatchProbeEndpoint }, transport);

            Assert.That(await auth.Ping(), Is.True);
            Assert.That(socket.Requests, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// The routing half of the predicate's truth table, with a stub registered so the latch holds: a service
        /// built on an endpoint goes to the registry exactly when <see cref="IhcSettings.IsStubConfigured"/> says
        /// stub, and is refused by the latch otherwise. One predicate decides both. A <c>mock://</c> row is
        /// refused already when the service is built, since nothing is registered under it.
        /// </summary>
        [TestCase("", TestName = "Routing(empty) is latched")]
        [TestCase("mock://latch-row", TestName = "Routing(unregistered mock key) is refused at construction")]
        [TestCase("http://row.controllerstub.invalid", TestName = "Routing(stub host) goes to the registry")]
        [TestCase("http://path-only.invalid/row.controllerstub.invalid", TestName = "Routing(suffix in the path only) is latched")]
        [TestCase("HTTP://ROW.ControllerStub.INVALID", TestName = "Routing(case variant) goes to the registry")]
        [TestCase("http://row.controllerstub.invalid@userinfo-probe.invalid", TestName = "Routing(user info hides the host) is latched")]
        [TestCase("https://row.controllerstub.invalid", TestName = "Routing(https) is latched")]
        public void EachEndpoint_GoesToTheRegistryExactlyWhenItIsAStubEndpoint(string endpoint)
        {
            using var registration = ControllerStubs.Register(new FailingStub());
            bool isStub = new IhcSettings { Endpoint = endpoint }.IsStubConfigured();

            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(async () =>
            {
                using var probe = ProbeOn(endpoint);
                await probe.Ping();
            });

            Assert.That(refusal!.ErrorCode, Is.EqualTo(isStub
                ? Errors.CONTROLLER_STUB_NOT_REGISTERED
                : Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED));
        }

        [Test]
        public void ALatchedCall_IsTracedWithTheLatchCode()
        {
            using var capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName, spanPrefix: "soapPost.");
            using var registration = ControllerStubs.Register(new FailingStub());

            AssertLatched(ProbeOn(LatchProbeEndpoint));

            Activity span = capture.Spans.Single();
            Assert.Multiple(() =>
            {
                Assert.That(span.StatusDescription, Does.StartWith(nameof(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED)),
                    "the failed span names the latch's code, which is how a latched call is found in telemetry");
                Assert.That(span.GetTagItem("error.type"), Is.EqualTo("_OTHER"));
            });
        }

        /// <summary>A ping through <paramref name="probe"/> is refused by the latch, and never sent.</summary>
        private static void AssertLatched(AuthenticationService probe)
        {
            using (probe)
            {
                var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => probe.Ping());
                Assert.Multiple(() =>
                {
                    Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED));
                    Assert.That(refusal.Message, Does.StartWith(nameof(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED) + ":"));
                });
            }
        }

        /// <summary>
        /// The one place these cases build a real service from settings, admitted in ControllerReachGuard as a
        /// latch probe: no credential, a reserved <c>.invalid</c> host, and ONE call the latch or the registry
        /// must refuse.
        /// </summary>
        private static AuthenticationService ProbeOn(string endpoint) => new(new IhcSettings { Endpoint = endpoint });

        /// <summary>A stub that refuses every call: these cases need a registration, never an answer.</summary>
        private sealed class FailingStub : IControllerStub
        {
            public Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken) =>
                throw new AssertionException("a latch case reached the stub that holds the latch");
        }
    }
}
