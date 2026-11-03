using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Envelope;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using NUnit.Framework;
using Api = Ihc.Soap.Openapi;
using Auth = Ihc.Soap.Authentication;
using Ctl = Ihc.Soap.Controller;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller-stub seam in the SDK: which endpoints name a stub, how a registration is owned, and that a
    /// real service built on a stub endpoint sends every call to that stub through the SDK's own handler chain,
    /// and to nothing else.
    /// </summary>
    /// <remarks>
    /// Non-parallelizable because the registry is process-wide: a case that counts what a registration received
    /// must not share the process with another case registering at the same time.
    /// </remarks>
    [TestFixture]
    [NonParallelizable]
    public class ControllerStubSeamTests
    {
        private const string SessionCookie = "JSESSIONID=STUBSEAM0001";

        /// <summary>A stub endpoint that no case registers and the generator never produces.</summary>
        private const string UnregisteredStubEndpoint = "http://unregistered.controllerstub.invalid";

        [Test]
        public async Task EachServiceBuiltOnARegisteredEndpoint_SendsItsCallsToThatStub()
        {
            var stub = new RecordingStub(Answer);
            using var registration = ControllerStubs.Register(stub);
            using var services = ServicesOn(registration.Endpoint);

            bool authPing = await services.Auth.Ping();
            bool sdCardReady = await services.Controller.IsSDCardReady();
            await services.OpenApi.Ping();

            Assert.Multiple(() =>
            {
                Assert.That(authPing && sdCardReady, Is.True, "each answer mapped back through the SDK");
                Assert.That(stub.Requests.Select(r => (r.Service, r.SoapAction)), Is.EqualTo(new[]
                {
                    ("AuthenticationService", "ping"),
                    ("ControllerService", "isSDCardReady"),
                    ("OpenAPIService", "ping"),
                }));
                Assert.That(stub.Requests.All(r => r.Body.Contains("Envelope", StringComparison.Ordinal)), Is.True,
                    "the stub receives the SOAP envelope the SDK built");
            });
        }

        /// <summary>
        /// A <c>mock://</c> endpoint no stub is registered for is refused when the service is BUILT, not on its
        /// first call: it comes from a settings file, and an application carries no stub, so the refusal is the
        /// application's startup error and its message is the repair - the endpoint it read, an edit of the
        /// settings file, or a start with the stub switch. Every wrapper reaches the check through the one base
        /// constructor, so one representative service stands for all of them.
        /// </summary>
        [Test]
        public void AnUnregisteredMockEndpoint_IsRefusedWhenTheServiceIsBuilt_WithBothWaysOut()
        {
            const string endpoint = SpecialEndpoints.MockedPrefix + "never-registered";

            var refusal = Assert.Throws<ErrorWithCodeException>(() => new AuthenticationService(new IhcSettings { Endpoint = endpoint }));

            Assert.Multiple(() =>
            {
                Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_NOT_REGISTERED));
                Assert.That(refusal.Message, Does.StartWith(nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED) + ":"));
                Assert.That(refusal.Message, Does.Contain(endpoint), "names the endpoint it read");
                Assert.That(refusal.Message, Does.Contain("settings file"), "names the edit that repairs it");
                Assert.That(refusal.Message, Does.Contain("--controller-stub"), "names the switch that repairs it");
                Assert.That(refusal.Message, Does.Contain(nameof(ControllerStubs) + "." + nameof(ControllerStubs.Register)),
                    "and what a test that named the endpoint itself does instead");
            });
        }

        [Test]
        public void AStubEndpointWithNoRegistration_IsRefusedWithItsCode_AndTracedAsAnUntypedError()
        {
            using var capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName, spanPrefix: "soapPost.");
            using var services = ServicesOn(UnregisteredStubEndpoint);

            ErrorWithCodeException refusal = PingRefusedAsUnregistered(services);

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Message, Does.Contain(UnregisteredStubEndpoint), "the refusal names the endpoint");
                Assert.That(refusal.Message, Does.Contain("Endpoint of a live registration"),
                    "and what a test does instead, since only a test can reach it with no registration");
                Activity span = capture.Spans.Single();
                Assert.That(span.StatusDescription, Does.StartWith(nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED)),
                    "the failed span names the refusal's code, which is how it is found in telemetry");
                Assert.That(span.GetTagItem("error.type"), Is.EqualTo("_OTHER"),
                    "a coded SDK error keeps the untyped error identity every coded error has");
            });
        }

        [Test]
        public void AMockKey_IsSingleUse_WhetherTheFirstRegistrationIsLiveOrDisposed()
        {
            var stub = new RecordingStub(Answer);
            var first = ControllerStubs.Register(stub, "mock://seam-single-use");

            var whileLive = Assert.Throws<ErrorWithCodeException>(() => ControllerStubs.Register(stub, "mock://seam-single-use"));
            first.Dispose();
            var afterDispose = Assert.Throws<ErrorWithCodeException>(() => ControllerStubs.Register(stub, "mock://seam-single-use"));

            Assert.Multiple(() =>
            {
                Assert.That(first.Endpoint, Is.EqualTo("mock://seam-single-use"));
                AssertCoded(whileLive, Errors.CONTROLLER_STUB_ENDPOINT_IN_USE, nameof(Errors.CONTROLLER_STUB_ENDPOINT_IN_USE));
                AssertCoded(afterDispose, Errors.CONTROLLER_STUB_ENDPOINT_IN_USE, nameof(Errors.CONTROLLER_STUB_ENDPOINT_IN_USE));
            });
        }

        /// <summary>
        /// A <c>mock://</c> key is a stub endpoint like a generated one once it is registered: a service builds on
        /// it, and its calls go to the stub registered under it.
        /// </summary>
        [Test]
        public async Task ARegisteredMockKey_BuildsAServiceThatRoutesToItsStub()
        {
            var stub = new RecordingStub(Answer);
            using var registration = ControllerStubs.Register(stub, "mock://seam-routes");
            using var services = ServicesOn(registration.Endpoint);

            bool pinged = await services.Auth.Ping();

            Assert.Multiple(() =>
            {
                Assert.That(pinged, Is.True);
                Assert.That(stub.Requests.Select(r => (r.Service, r.SoapAction)),
                    Is.EqualTo(new[] { ("AuthenticationService", "ping") }));
            });
        }

        [Test]
        public void GeneratedEndpoints_AreStubEndpoints_AndNeverRepeat()
        {
            var stub = new RecordingStub(Answer);
            using var one = ControllerStubs.Register(stub);
            using var two = ControllerStubs.Register(stub);

            Assert.Multiple(() =>
            {
                Assert.That(one.Endpoint, Is.Not.EqualTo(two.Endpoint));
                Assert.That(new IhcSettings { Endpoint = one.Endpoint }.IsStubConfigured(), Is.True);
                Assert.That(new Uri(one.Endpoint).Host, Does.EndWith(".invalid"), "a reserved host that never resolves");
            });
        }

        [Test]
        public async Task AfterDispose_ACallThroughAServiceBuiltOnTheRegistration_IsRefused()
        {
            var stub = new RecordingStub(Answer);
            var registration = ControllerStubs.Register(stub);
            using var services = ServicesOn(registration.Endpoint);
            Assert.That(await services.Auth.Ping(), Is.True, "routed while the registration was live");

            registration.Dispose();

            PingRefusedAsUnregistered(services);
            Assert.That(stub.Requests, Has.Count.EqualTo(1), "nothing reached the stub after its registration ended");
        }

        [Test]
        public async Task DisposingARegistrationTwice_LeavesAnotherRegistrationLive()
        {
            var stub = new RecordingStub(Answer);
            var disposedTwice = ControllerStubs.Register(stub);
            using var other = ControllerStubs.Register(stub);
            using var services = ServicesOn(other.Endpoint);

            disposedTwice.Dispose();
            disposedTwice.Dispose();

            Assert.That(await services.Auth.Ping(), Is.True,
                "a second Dispose removed nothing but its own registration, which had already left");
        }

        [TestCase("http://chosen.invalid")]
        [TestCase("http://s1.controllerstub.invalid")]
        [TestCase("mock://")]
        [TestCase("mock:///")]
        public void AChosenKeyThatIsNotANamedMockKey_IsRefused(string endpoint)
        {
            var refusal = Assert.Throws<ErrorWithCodeException>(
                () => ControllerStubs.Register(new RecordingStub(Answer), endpoint));

            Assert.Multiple(() =>
                AssertCoded(refusal, Errors.CONTROLLER_STUB_ENDPOINT_INVALID, nameof(Errors.CONTROLLER_STUB_ENDPOINT_INVALID)));
        }

        /// <summary>
        /// A ping through <paramref name="services"/> is refused because no stub is registered for their endpoint,
        /// and never sent.
        /// </summary>
        private static ErrorWithCodeException PingRefusedAsUnregistered(Services services)
        {
            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => services.Auth.Ping());
            AssertCoded(refusal, Errors.CONTROLLER_STUB_NOT_REGISTERED, nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED));
            return refusal!;
        }

        /// <summary>
        /// A seam refusal carries <paramref name="code"/>, and its message starts with the code's name, which is
        /// how the refusal is found in telemetry, where every coded error shares one untyped error identity.
        /// </summary>
        private static void AssertCoded(ErrorWithCodeException? refusal, int code, string codeName)
        {
            Assert.That(refusal, Is.Not.Null, "refused with a coded error");
            Assert.That(refusal!.ErrorCode, Is.EqualTo(code));
            Assert.That(refusal.Message, Does.StartWith(codeName + ":"));
        }

        /// <summary>
        /// A refusal assertion compares codes, so two equal codes would let it pass on the wrong refusal.
        /// </summary>
        [Test]
        public void TheErrorCodes_ArePairwiseDistinct()
        {
            var codes = typeof(Errors)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(int))
                .ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);

            Assert.Multiple(() =>
            {
                Assert.That(codes.Values, Is.Unique);
                Assert.That(codes.Keys, Is.SupersetOf(new[]
                {
                    nameof(Errors.CONTROLLER_STUB_NOT_REGISTERED),
                    nameof(Errors.CONTROLLER_SOCKET_REFUSED_WHILE_STUBBED),
                    nameof(Errors.CONTROLLER_STUB_FAULTED),
                    nameof(Errors.CONTROLLER_STUB_ENDPOINT_INVALID),
                    nameof(Errors.CONTROLLER_STUB_IDENTITY_FAILED),
                    nameof(Errors.CONTROLLER_STUB_ENDPOINT_IN_USE),
                }), "the reflection reaches the seam's codes, so their distinctness is actually checked");
            });
        }

        [Test]
        public async Task AStubEndpoint_WinsOverAnExplicitTransport()
        {
            var stub = new RecordingStub(Answer);
            using var registration = ControllerStubs.Register(stub);
            var socket = new StubTransport(_ => throw new AssertionException("a stub endpoint reached the explicit transport"));
            using var transport = Client.CreateHttpClient(socket);
            using var auth = new AuthenticationService(StubSettings(registration.Endpoint), transport);

            bool pinged = await auth.Ping();

            Assert.Multiple(() =>
            {
                Assert.That(pinged, Is.True);
                Assert.That(stub.Requests, Has.Count.EqualTo(1));
                Assert.That(socket.Requests, Is.Empty);
            });
        }

        /// <summary>
        /// Routing reads the address the service was built for, never the settings' endpoint, which a host may
        /// rewrite in place after construction. Moving the endpoint away does not take a stub-built service off
        /// its stub.
        /// </summary>
        [Test]
        public async Task AnEndpointChangedAfterConstruction_DoesNotTakeAServiceOffItsStub()
        {
            var stub = new RecordingStub(Answer);
            using var registration = ControllerStubs.Register(stub);
            using var services = ServicesOn(registration.Endpoint);

            services.Auth.IhcSettings.Endpoint = "http://moved-away.invalid";
            bool pinged = await services.Auth.Ping();

            Assert.Multiple(() =>
            {
                Assert.That(pinged, Is.True);
                Assert.That(stub.Requests, Has.Count.EqualTo(1), "the call still went to the stub");
            });
        }

        /// <summary>
        /// The other direction: moving the endpoint ONTO a stub does not take a service built for another address
        /// to that stub. Its explicit transport stands in for the socket, so nothing here can reach a network.
        /// </summary>
        [Test]
        public async Task AnEndpointChangedToAStubAfterConstruction_DoesNotRouteAServiceToTheStub()
        {
            var stub = new RecordingStub(Answer);
            using var registration = ControllerStubs.Register(stub);
            var socket = new StubTransport(_ => Answering(Envelope(new Auth.outputMessageName3(true)), setCookie: null));
            using var transport = Client.CreateHttpClient(socket);
            using var auth = new AuthenticationService(StubSettings("http://not-a-stub.invalid"), transport);

            auth.IhcSettings.Endpoint = registration.Endpoint;
            bool pinged = await auth.Ping();

            Assert.Multiple(() =>
            {
                Assert.That(pinged, Is.True);
                Assert.That(socket.Requests, Has.Count.EqualTo(1), "the call went where the service was built for");
                Assert.That(stub.Requests, Is.Empty);
            });
        }

        /// <summary>
        /// The predicate rows. Every row's routing half, where a stub endpoint must go to the registry and every
        /// other endpoint must be refused while a stub is live, belongs with the latch.
        /// </summary>
        [TestCase("", false, TestName = "IsStubConfigured(empty) is false")]
        [TestCase(SpecialEndpoints.Usb, false, TestName = "IsStubConfigured(usb) is false")]
        [TestCase("mock://x", true, TestName = "IsStubConfigured(mock key) is true")]
        [TestCase("http://s1.controllerstub.invalid", true, TestName = "IsStubConfigured(stub host) is true")]
        [TestCase("http://path-only.invalid/s1.controllerstub.invalid", false, TestName = "IsStubConfigured(suffix in the path only) is false")]
        [TestCase("HTTP://S1.ControllerStub.INVALID", true, TestName = "IsStubConfigured(case variant) is true")]
        [TestCase("http://s1.controllerstub.invalid@userinfo-probe.invalid", false, TestName = "IsStubConfigured(user info hides the host) is false")]
        [TestCase("https://s1.controllerstub.invalid", false, TestName = "IsStubConfigured(https) is false")]
        public void IsStubConfigured_AnswersForTheEndpoint(string endpoint, bool expected)
        {
            Assert.That(new IhcSettings { Endpoint = endpoint }.IsStubConfigured(), Is.EqualTo(expected));
        }

        /// <summary>
        /// The session cookie travels through the seam in both directions: the login presents none, its answer
        /// sets one, and the next call presents what it set.
        /// </summary>
        [Test]
        public async Task TheSessionCookie_RoundTripsThroughTheStub()
        {
            var stub = new RecordingStub(Answer);
            using var registration = ControllerStubs.Register(stub);
            using (var services = ServicesOn(registration.Endpoint))
            {
                await services.Auth.Authenticate();
                await services.Auth.Ping();
            }

            ControllerStubRequest login = stub.Requests.Single(r => r.SoapAction == "authenticate");
            ControllerStubRequest ping = stub.Requests.Single(r => r.SoapAction == "ping");
            Assert.Multiple(() =>
            {
                Assert.That(login.Cookie, Is.Null, "a login opens a session, so it presents no cookie");
                Assert.That(ping.Cookie, Is.EqualTo(SessionCookie), "the next call presents the cookie the login set");
            });
        }

        /// <summary>
        /// A stub's deliberate connection loss reaches the caller as a transport failure. Only the exception type
        /// is promised, never a finer reason, so nothing else about it is asserted.
        /// </summary>
        [Test]
        public async Task AConnectionLostAnswer_FailsTheCallAsATransportFailure()
        {
            Exception? failure = await PingFailure(_ => ControllerStubResponse.ConnectionLost);

            Assert.That(failure, Is.TypeOf<HttpRequestException>());
        }

        [Test]
        public async Task ACancellationInsideTheStub_StaysACancellation()
        {
            Exception? failure = await PingFailure(_ => throw new OperationCanceledException("the stub's call was cancelled"));

            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        }

        /// <summary>
        /// A stub that throws is broken, and the call says so with its own code rather than passing the stub's
        /// exception off as an answer or a transport failure.
        /// </summary>
        [Test]
        public async Task AStubThatThrows_FailsTheCallAsAStubDefect()
        {
            Exception? failure = await PingFailure(_ => throw new InvalidOperationException("broken stub"));

            Assert.Multiple(() =>
            {
                AssertCoded(failure as ErrorWithCodeException, Errors.CONTROLLER_STUB_FAULTED, nameof(Errors.CONTROLLER_STUB_FAULTED));
                Assert.That(failure?.InnerException, Is.TypeOf<InvalidOperationException>(), "the defect is carried");
            });
        }

        [Test]
        public async Task AStubThatReturnsNoResponse_FailsTheCallAsAStubDefect()
        {
            Exception? failure = await PingFailure(_ => null!);

            AssertCoded(failure as ErrorWithCodeException, Errors.CONTROLLER_STUB_FAULTED, nameof(Errors.CONTROLLER_STUB_FAULTED));
        }

        /// <summary>
        /// Nothing but the registry references the registration once the helper returns, and a forced collection
        /// still leaves it routing: dropping a registration never releases it. The helper is kept out of line,
        /// because a local the JIT keeps reachable in this method would make the case unable to fail; the weak
        /// handle does not keep it alive either. Disposing it then refuses the same endpoint, which shows the case
        /// can see a registration go.
        /// </summary>
        [Test]
        public async Task ADroppedRegistration_StaysLiveThroughACollection_UntilItIsDisposed()
        {
            string endpoint = RegisterAndDrop(new RecordingStub(Answer), out WeakReference<ControllerStubRegistration> handle);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using var services = ServicesOn(endpoint);
            Assert.That(await services.Auth.Ping(), Is.True, "a dropped registration still routes");
            Assert.That(handle.TryGetTarget(out ControllerStubRegistration? registration), Is.True,
                "the registry holds the registration strongly");

            registration!.Dispose();

            PingRefusedAsUnregistered(services);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string RegisterAndDrop(IControllerStub stub, out WeakReference<ControllerStubRegistration> handle)
        {
            var registration = ControllerStubs.Register(stub);
            handle = new WeakReference<ControllerStubRegistration>(registration);
            return registration.Endpoint;
        }

        /// <summary>
        /// A long poll the stub holds open ends when its registration is disposed, rather than outliving the stub
        /// it was routed to; the call after it is refused.
        /// </summary>
        [Test]
        public async Task DisposingARegistration_EndsACallItsStubIsHolding()
        {
            var stub = new HoldingStub();
            var registration = ControllerStubs.Register(stub);
            using var services = ServicesOn(registration.Endpoint);
            Task<IReadOnlyList<ResourceValue>> held = services.Resources.WaitForResourceValueChanges(holdSeconds: 1);
            await stub.Holding.WaitAsync(TimeSpan.FromSeconds(10));

            registration.Dispose();

            Exception? ended = Assert.CatchAsync(async () => await held.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(ended, Is.InstanceOf<OperationCanceledException>(), "the held call was cancelled, not timed out");
            PingRefusedAsUnregistered(services);
        }

        /// <summary>Pings once through a fresh registration of a stub answering with
        /// <paramref name="answer"/>, and returns what the ping threw, or null when it succeeded.</summary>
        private static async Task<Exception?> PingFailure(Func<ControllerStubRequest, ControllerStubResponse> answer)
        {
            using var registration = ControllerStubs.Register(new RecordingStub(answer));
            using var services = ServicesOn(registration.Endpoint);
            try
            {
                await services.Auth.Ping();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// The one place these cases build real services from settings. It is admitted in ControllerReachGuard,
        /// because its services DO call: their endpoint is a stub endpoint, which the SDK routes only to a
        /// registration and refuses when none is registered, never to a socket.
        /// </summary>
        private static Services ServicesOn(string stubEndpoint)
        {
            IhcSettings settings = StubSettings(stubEndpoint);
            var auth = new AuthenticationService(settings);
            return new Services(auth, new ControllerService(auth), new OpenAPIService(settings),
                new ResourceInteractionService(auth));
        }

        private static IhcSettings StubSettings(string endpoint) =>
            new() { Endpoint = endpoint, UserName = "stub-user", Password = "stub-only" };

        private sealed record Services(AuthenticationService Auth, ControllerService Controller, OpenAPIService OpenApi,
            ResourceInteractionService Resources) : IDisposable
        {
            // Only the authentication service holds a session to end; disposing it logs out a connected one.
            public void Dispose() => Auth.Dispose();
        }

        /// <summary>A controller's answer to each exchange these cases send.</summary>
        private static ControllerStubResponse Answer(ControllerStubRequest request) => (request.Service, request.SoapAction) switch
        {
            ("AuthenticationService", "authenticate") => new(200, Envelope(new Auth.outputMessageName2(
                new Auth.WSLoginResult { loginWasSuccessful = true, loggedInUser = new Auth.WSUser { username = "stub-user" } })),
                SessionCookie),
            ("AuthenticationService", "ping") => new(200, Envelope(new Auth.outputMessageName3(true))),
            ("AuthenticationService", "disconnect") => new(200, Envelope(new Auth.outputMessageName1(true))),
            ("ControllerService", "isSDCardReady") => new(200, Envelope(new Ctl.outputMessageName9(true))),
            ("OpenAPIService", "ping") => new(200, Envelope(new Api.outputMessageName10())),
            _ => throw new AssertionException($"no answer scripted for {request.Service}.{request.SoapAction}"),
        };

        /// <summary>A SOAP answer carrying <paramref name="body"/>, written by the SDK's own serializer.</summary>
        private static string Envelope<T>(T body) => Serialization.SerializeXml(new ResponseEnvelope<T>(body));

        private static HttpResponseMessage Answering(string body, string? setCookie)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };
            if (setCookie is not null)
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
            }
            return response;
        }

        /// <summary>A stub that holds every call open until the call is cancelled, as a long poll waits.</summary>
        private sealed class HoldingStub : IControllerStub
        {
            private readonly TaskCompletionSource holding = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Completes once a call is being held.</summary>
            internal Task Holding => holding.Task;

            public async Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken)
            {
                holding.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new AssertionException("only a cancellation ends a held call");
            }
        }

        /// <summary>A stub that answers as scripted and records every exchange it received, in order.</summary>
        private sealed class RecordingStub(Func<ControllerStubRequest, ControllerStubResponse> answer) : IControllerStub
        {
            private readonly ConcurrentQueue<ControllerStubRequest> requests = new();

            internal IReadOnlyList<ControllerStubRequest> Requests => [.. requests];

            public Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken)
            {
                requests.Enqueue(request);
                return Task.FromResult(answer(request));
            }
        }
    }
}
