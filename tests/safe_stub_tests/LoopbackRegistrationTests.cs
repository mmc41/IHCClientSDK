using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using Ihc.Tests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// A registration routed to a stub host in another process: what its address must be, the identity check it makes
    /// before it exists, how a caller's cancellation of that check differs from a host that fails it, the locks on the
    /// client that alone reaches the host, and a round trip that the stub journals exactly as it journals the same calls
    /// in-process.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LoopbackRegistrationTests
    {
        private static readonly ProjectFile Uploaded = new("uploaded.vis",
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\r\n<utcs_project>\r\n  <name>Køkken æøå ÆØÅ</name>\n</utcs_project>\r\n");

        [Test]
        public async Task ASuccessfulRegistration_HasAlreadyCheckedTheHostsIdentity()
        {
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ControllerStub(), CancellationToken.None);
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            using ControllerStubRegistration registration = await ControllerStubs.RegisterLoopbackAsync(host.BaseAddress, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(host.IdentifyRequests, Is.EqualTo(1), "checked before any service exists");
                Assert.That(RequestDestinations.SdkRequests(capture.Spans).Select(span => span.GetTagItem("url.full")),
                    Is.EqualTo(new[] { new Uri(host.BaseAddress, "identify").ToString() }),
                    "the check is one SDK request, traced on the host's own address");
                Assert.That(new IhcSettings { Endpoint = registration.Endpoint }.IsStubConfigured(), Is.True,
                    "the registration's endpoint is a stub endpoint, like an in-process one");
            });
        }

        /// <summary>The same store-then-get over both transports leaves the same journal, and over loopback every SDK
        /// span still names the stub endpoint, because the rewrite to the host sits below the SDK's tracing.</summary>
        [Test]
        public async Task AOneCallStoreThenGet_OverLoopback_IsJournalledAsInProcess()
        {
            IReadOnlyList<(string, string)> inProcess;
            using (var sdk = StubbedSdk.Start())
            {
                await StoreThenGet(sdk);
                inProcess = Journalled(sdk.Stub);
            }

            var stub = new ControllerStub();
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(stub, CancellationToken.None);
            using (var sdk = StubbedSdk.Start(stub, await ControllerStubs.RegisterLoopbackAsync(host.BaseAddress, CancellationToken.None)))
            {
                using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

                await StoreThenGet(sdk);

                IReadOnlyList<Activity> requests = RequestDestinations.SdkRequests(capture.Spans);
                Assert.Multiple(() =>
                {
                    Assert.That(Journalled(stub), Is.EqualTo(inProcess));
                    Assert.That(requests, Has.Count.EqualTo(stub.Journal.All.Count), "one SDK request per exchange");
                    Assert.That(RequestDestinations.NonStubDestinations(requests), Is.Empty,
                        "SDK spans name the stub endpoint, not the host");
                });
            }
        }

        /// <summary>Every address shape a loopback registration refuses, and so the switch refuses too.</summary>
        internal static IEnumerable<TestCaseData> NotALoopbackStubHost()
        {
            yield return new TestCaseData("http://localhost:50123/token/").SetArgDisplayNames("localhost");
            yield return new TestCaseData("http://192.168.1.10:50123/token/").SetArgDisplayNames("a LAN address");
            yield return new TestCaseData("http://stub.example.invalid:50123/token/").SetArgDisplayNames("a DNS name");
            yield return new TestCaseData("http://127.0.0.1/token/").SetArgDisplayNames("no port");
            yield return new TestCaseData("http://127.0.0.1:50123/").SetArgDisplayNames("no token");
            yield return new TestCaseData("https://127.0.0.1:50123/token/").SetArgDisplayNames("https");
            yield return new TestCaseData("http://user@127.0.0.1:50123/token/").SetArgDisplayNames("user info");
            yield return new TestCaseData("http://127.0.0.1:50123/token/?x=1").SetArgDisplayNames("a query");
            yield return new TestCaseData("/token/").SetArgDisplayNames("a relative address");
        }

        /// <summary>An address is no usage error but input, so its refusal is stored in the task: the call itself
        /// returns.</summary>
        [TestCaseSource(nameof(NotALoopbackStubHost))]
        public void AnAddressThatIsNotALoopbackStubHost_IsRefused_BeforeAnythingIsSent(string address)
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            Task<ControllerStubRegistration> registering =
                ControllerStubs.RegisterLoopbackAsync(new Uri(address, UriKind.RelativeOrAbsolute), CancellationToken.None);
            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => registering);

            Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_ENDPOINT_INVALID));
            Assert.That(RequestDestinations.SdkRequests(capture.Spans), Is.Empty);
        }

        /// <summary>A caller that cancels while the host has not answered yet gets a cancelled task carrying its own
        /// token, never the identity failure a silent host earns at the limit, and no registration latches the
        /// process.</summary>
        [Test]
        public async Task ACallersCancellation_EndsTheRegistrationCancelled_NotAsAnIdentityFailure()
        {
            var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, _) => HoldUnanswered(context, asked));
            using var cancellation = new CancellationTokenSource();

            Task<ControllerStubRegistration> registering =
                ControllerStubs.RegisterLoopbackAsync(new Uri(server.Address + "/token/"), cancellation.Token);
            await WhenAsked(asked.Task, registering);
            await cancellation.CancelAsync();

            var cancelled = Assert.CatchAsync<OperationCanceledException>(() => registering);
            Assert.Multiple(() =>
            {
                Assert.That(registering.IsCanceled, Is.True, "cancelled, not faulted");
                Assert.That(cancelled!.CancellationToken, Is.EqualTo(cancellation.Token), "by the caller's own token");
                Assert.That(ControllerStubRegistry.AnyLive, Is.False, "and nothing was registered");
            });
        }

        /// <summary>A token cancelled before the call gets a cancelled task, and not even the identity check is
        /// sent.</summary>
        [Test]
        public async Task AnAlreadyCancelledToken_EndsTheRegistrationCancelled_BeforeAnythingIsSent()
        {
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, _) =>
                context.Response.WriteAsync("never asked"));

            Task<ControllerStubRegistration> registering =
                ControllerStubs.RegisterLoopbackAsync(new Uri(server.Address + "/token/"), new CancellationToken(canceled: true));

            Assert.That(async () => await registering, Throws.InstanceOf<OperationCanceledException>());
            Assert.That((registering.IsCanceled, server.Requests), Is.EqualTo((true, 0)));
        }

        /// <summary>The control for the cancellation case above: the same silent host, and a caller that could cancel
        /// but does not, so the limit ends the check, which faults the task as an identity failure that says nothing was
        /// sent. Run through the identity check itself on a fake clock, so the five-second limit passes without a
        /// wait.</summary>
        [Test]
        public async Task AHostStillSilentAtTheLimit_IsAnIdentityFailure_NotACancellation()
        {
            var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, _) => HoldUnanswered(context, asked));
            var loopbackBase = new Uri(server.Address + "/token/");
            using HttpClient client = ControllerStubLoopback.CreateClient(loopbackBase, "http://s0.controllerstub.invalid");
            using var cancellation = new CancellationTokenSource();
            var clock = new FakeTimeProvider();

            Task checking = ControllerStubLoopback.PreflightAsync(client, loopbackBase, clock, cancellation.Token);
            await WhenAsked(asked.Task, checking);
            clock.Advance(ControllerStubLoopback.PreflightLimit);
            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => checking);

            Assert.Multiple(() =>
            {
                Assert.That(checking.IsFaulted, Is.True, "faulted, not cancelled");
                Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_IDENTITY_FAILED));
                Assert.That(refusal.Message, Does.EndWith(ControllerStubLoopback.NothingSent));
            });
        }

        /// <summary>The control for the table above: the other loopback literals pass validation, so the table does
        /// not pass by refusing everything.</summary>
        [TestCase("http://127.0.0.1:50123/token/")]
        [TestCase("http://127.5.6.7:50123/token")]
        [TestCase("http://[::1]:50123/token/")]
        public void ALoopbackLiteralWithPortAndToken_PassesValidation(string address) =>
            Assert.That(ControllerStubLoopback.ValidatedBase(new Uri(address)).AbsolutePath, Is.EqualTo("/token/"));

        [Test]
        public void TheClientsSocketHandler_HasNoProxyNoRedirectNoCookieContainer_AndAConnectGate()
        {
            using var handler = ControllerStubLoopback.CreatePrimaryHandler();

            Assert.That((handler.UseProxy, handler.AllowAutoRedirect, handler.UseCookies, handler.ConnectCallback is null),
                Is.EqualTo((false, false, false, false)));
        }

        /// <summary>The connect gate, which the address validation makes unreachable through the public API, so the
        /// client is built directly: a host that is not a loopback literal is refused before it is resolved.</summary>
        [Test]
        public void TheClient_RefusesANonLoopbackRemote_WithoutResolvingIt()
        {
            using HttpClient client = ControllerStubLoopback.CreateClient(new Uri("http://127.0.0.1:50123/token/"),
                "http://s0.controllerstub.invalid");
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://probe.invalid:9/");

            var failure = Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request));

            Assert.That(Chain(failure).OfType<ErrorWithCodeException>().Select(e => e.ErrorCode),
                Does.Contain(Errors.CONTROLLER_STUB_ENDPOINT_INVALID),
                "refused by the gate, not by a failed name lookup");
        }

        /// <summary>A server that redirects the identity check to an address that WOULD prove it gets one request:
        /// were the redirect followed, the registration would succeed.</summary>
        [Test]
        public async Task AnIdentityCheckThatRedirects_IsNotFollowed()
        {
            const string token = "redirected";
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, self) =>
            {
                context.Response.Headers[ControllerStubLoopback.IdentityHeader] = token;
                if (context.Request.Path.Value!.EndsWith("/identify", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status302Found;
                    context.Response.Headers.Location = self.Address + "/" + token + "/proof";
                    return Task.CompletedTask;
                }
                return context.Response.WriteAsync(ControllerStubLoopback.IdentityAnswerPrefix + token);
            });

            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() =>
                ControllerStubs.RegisterLoopbackAsync(new Uri(server.Address + "/" + token + "/"), CancellationToken.None));

            Assert.That((refusal!.ErrorCode, server.Requests), Is.EqualTo((Errors.CONTROLLER_STUB_IDENTITY_FAILED, 1)));
        }

        /// <summary>Says the identity check has arrived, then keeps it unanswered until the client gives up on it, by
        /// cancelling or at its limit, and ends then, so the server stops without waiting.</summary>
        private static async Task HoldUnanswered(HttpContext context, TaskCompletionSource asked)
        {
            asked.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // The client went away, which is what the case waits for.
            }
        }

        /// <summary>Waits until the identity check has reached the host, so what the case does next happens while the
        /// host is holding it; a check that ended before that fails the case.</summary>
        private static async Task WhenAsked(Task asked, Task checking)
        {
            await Task.WhenAny(asked, checking);
            Assert.That(asked.IsCompleted, Is.True, "the identity check reached the host before it ended");
        }

        private static async Task StoreThenGet(StubbedSdk sdk)
        {
            Assert.That(await sdk.Controller.StoreProject(Uploaded), Is.True);
            ProjectFile? fetched = await sdk.Controller.GetProject();
            Assert.That(ProjectFile.Encoding.GetBytes(fetched!.Data), Is.EqualTo(ProjectFile.Encoding.GetBytes(Uploaded.Data)));
        }

        private static IReadOnlyList<(string, string)> Journalled(ControllerStub stub) =>
            [.. stub.Journal.All.Select(call => (call.Service, call.SoapAction))];

        private static IEnumerable<Exception> Chain(Exception? exception)
        {
            for (Exception? at = exception; at is not null; at = at.InnerException)
            {
                yield return at;
            }
        }
    }
}
