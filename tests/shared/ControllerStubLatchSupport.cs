using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// A controller stub that fails every exchange with HTTP 500. It exists to HOLD the latch for a whole test run,
    /// not to answer: nothing is built on its endpoint, so a call reaching it means a test did something no
    /// controller-free suite should. It needs nothing but ihcclient, so a suite that links it gains no reference to
    /// the stub host and its web framework.
    /// </summary>
    internal sealed class FailAllControllerStub : IControllerStub
    {
        private const string Fault =
            "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><s:Fault>"
            + "<faultcode>s:Server</faultcode>"
            + "<faultstring>The test run's latch stub answers no call.</faultstring>"
            + "</s:Fault></s:Body></s:Envelope>";

        public Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ControllerStubResponse(500, Fault));
    }

    /// <summary>
    /// The positive control for the run-wide latch. Each suite builds one service on <see cref="Endpoint"/> in its
    /// OWN namespace and hands it here: a probe in the latch fixture's namespace would pass even where the fixture
    /// covers nothing else.
    /// </summary>
    internal static class ControllerLatchProbe
    {
        /// <summary>
        /// A real endpoint, never a stub one, under the reserved <c>.invalid</c> name: should the latch regress,
        /// the call ends in a transport failure for a name that never resolves, never at a device.
        /// </summary>
        internal const string Endpoint = "http://latch-probe.invalid";

        /// <summary>
        /// Calls one operation through <paramref name="probe"/>, which must be built on <see cref="Endpoint"/> with
        /// no credential, and requires the latch's refusal. Disposes the probe.
        /// </summary>
        internal static void RequireRefused(AuthenticationService probe)
        {
            using (probe)
            {
                var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => probe.Ping(),
                    "every test in this suite must run latched: a real endpoint has to be refused before any socket");
                Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED),
                    "the latch's own refusal, not some other failure that would also keep the call off the network");
            }
        }
    }
}
