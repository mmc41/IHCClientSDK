using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Stubbing;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// How a loopback registration refuses what is not its stub host, and how its calls fail and carry the session
    /// cookie: the same as an in-process registration's wherever both transports have the case, which each such case
    /// runs over both.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LoopbackTransportTests
    {
        public enum Transport { InProcess, Loopback }

        public enum Unanswerable { StatusBelowTheFinalRange, StatusAboveIt, NoBody }

        private const string GetState = "getState";

        [Test]
        public async Task AHostAnsweringUnderAnotherToken_IsRefused_AndHearsNoSoapRequest()
        {
            var stub = new ControllerStub();
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(stub, CancellationToken.None);
            var wrongToken = new Uri(host.BaseAddress.GetLeftPart(UriPartial.Authority) + "/not-the-token/");

            AssertIdentityRefused(ControllerStubs.RegisterLoopbackAsync(wrongToken, CancellationToken.None));

            Assert.That((host.IdentifyRequests, stub.Journal.All.Count), Is.EqualTo((0, 0)));
        }

        [Test]
        public async Task APlainLocalServer_IsRefused_AfterOnlyTheIdentityCheck()
        {
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, _) =>
                context.Response.WriteAsync("an ordinary local web server"));

            AssertIdentityRefused(ControllerStubs.RegisterLoopbackAsync(new Uri(server.Address + "/token/"), CancellationToken.None));

            Assert.That(server.Requests, Is.EqualTo(1), "the identity check, and no SOAP request");
        }

        [Test]
        public async Task AnAddressNothingListensOn_IsRefused()
        {
            string address;
            await using (LoopbackTestServer gone = await LoopbackTestServer.StartAsync((_, _) => Task.CompletedTask))
            {
                address = gone.Address;
            }

            AssertIdentityRefused(ControllerStubs.RegisterLoopbackAsync(new Uri(address + "/token/"), CancellationToken.None));
        }

        /// <summary>An identity check answered with the right status and token, but with the stub header missing or
        /// given twice: the host writes it exactly once, so either is an identity failure at registration, never an
        /// exception of its own.</summary>
        [Test]
        public async Task AnIdentityCheckWithoutTheStubHeaderExactlyOnce_IsRefused([Values(0, 2)] int headers)
        {
            const string token = "header-count";
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, _) =>
            {
                WriteStubHeader(context, token, headers);
                return context.Response.WriteAsync(ControllerStubLoopback.IdentityAnswerPrefix + token);
            });

            AssertIdentityRefused(ControllerStubs.RegisterLoopbackAsync(new Uri(server.Address + "/" + token + "/"), CancellationToken.None));

            Assert.That(server.Requests, Is.EqualTo(1), "the identity check, and no SOAP request");
        }

        /// <summary>A server that proves itself at registration and then answers a SOAP request without the stub
        /// header exactly once: whoever answered that request is not established, so the call fails after the one
        /// request, and the refusal says the request was sent rather than claiming nothing was.</summary>
        [Test]
        public async Task AnAnswerWithoutTheStubHeaderExactlyOnce_FailsTheCall_AfterOneSoapRequest([Values(0, 2)] int headers)
        {
            const string token = "proves-once";
            int soapRequests = 0;
            await using LoopbackTestServer server = await LoopbackTestServer.StartAsync((context, _) =>
            {
                if (context.Request.Path.Value!.EndsWith("/identify", StringComparison.Ordinal))
                {
                    WriteStubHeader(context, token, 1);
                    return context.Response.WriteAsync(ControllerStubLoopback.IdentityAnswerPrefix + token);
                }
                Interlocked.Increment(ref soapRequests);
                WriteStubHeader(context, token, headers);
                return context.Response.WriteAsync("<not-checked/>");
            });

            using var sdk = StubbedSdk.Start(new ControllerStub(),
                await ControllerStubs.RegisterLoopbackAsync(new Uri(server.Address + "/" + token + "/"), CancellationToken.None));
            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => sdk.Authentication.Ping());

            Assert.Multiple(() =>
            {
                Assert.That((refusal!.ErrorCode, soapRequests), Is.EqualTo((Errors.CONTROLLER_STUB_IDENTITY_FAILED, 1)));
                Assert.That(refusal.Message, Does.EndWith(ControllerStubLoopback.SentBeforeChecked));
            });
        }

        /// <summary>A lost connection is a transport failure on both transports, and the stub journals the call once:
        /// the loopback client does not resend it. Only the exception type is promised.</summary>
        [Test]
        public async Task AConnectionLost_FailsAsATransportFailure_AndIsJournalledOnce([Values] Transport transport)
        {
            var stub = new ControllerStub();
            stub.FailOn<IControllerService>(nameof(IControllerService.GetControllerState), StubFault.ConnectionLost);
            var (sdk, host) = await Start(transport, stub);
            try
            {
                Assert.That(async () => await sdk.Controller.GetControllerState(), Throws.TypeOf<HttpRequestException>());
                Assert.That(stub.Journal.All.Select(call => call.SoapAction), Is.EqualTo(new[] { GetState }));
            }
            finally
            {
                await Stop(sdk, host);
            }
        }

        /// <summary>A stub that throws fails the call with the defect code on both transports, and the exception is
        /// among the stub's defects, which the teardown check then reports.</summary>
        [Test]
        public async Task AThrowingStub_FailsTheCallAsADefect_AndIsRecorded([Values] Transport transport)
        {
            IReadOnlyList<StubDefect> defects = await DefectsOfAFaultedCall(transport,
                (_, _) => throw new NotSupportedException("broken stub"));

            Assert.That(defects.Select(defect => (defect.SoapAction, defect.Exception.GetType())),
                Is.EqualTo(new[] { (GetState, typeof(NotSupportedException)) }));
        }

        /// <summary>An answer no HTTP response carries, a status outside 200 to 599 or no body, is the stub's defect on
        /// both transports. It is refused where the stub builds it, so the call fails with the defect code and the stub
        /// records it, rather than reaching the caller as a controller's status, a transport failure, or an identity
        /// failure that blames the host.</summary>
        [Test]
        public async Task AnAnswerNoResponseCarries_FailsTheCallAsADefect_AndIsRecorded([Values] Transport transport,
            [Values] Unanswerable answer)
        {
            IReadOnlyList<StubDefect> defects = await DefectsOfAFaultedCall(transport, (_, _) => Task.FromResult(answer switch
            {
                Unanswerable.StatusBelowTheFinalRange => new ControllerStubResponse(199, "<not-soap/>"),
                Unanswerable.StatusAboveIt => new ControllerStubResponse(600, "<not-soap/>"),
                _ => new ControllerStubResponse(200, null!),
            }));

            Assert.That(defects.Select(defect => defect.SoapAction), Is.EqualTo(new[] { GetState }));
        }

        /// <summary>Answers <c>getState</c> with <paramref name="answer"/> over <paramref name="transport"/>, asserts that
        /// the call fails with the defect code and that the teardown check reports it, and returns the stub's defects as
        /// they stood before the teardown.</summary>
        private static async Task<IReadOnlyList<StubDefect>> DefectsOfAFaultedCall(Transport transport,
            Func<ControllerStubRequest, CancellationToken, Task<ControllerStubResponse>> answer)
        {
            var stub = new ControllerStub();
            stub.OverrideRaw("ControllerService", GetState, answer);
            var (sdk, host) = await Start(transport, stub);
            try
            {
                var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => sdk.Controller.GetControllerState());
                Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_FAULTED));
                return stub.Defects;
            }
            finally
            {
                Assert.That(() => sdk.Dispose(), Throws.InvalidOperationException, "the teardown check reports the defect");
                if (host is not null)
                {
                    await host.DisposeAsync();
                }
            }
        }

        /// <summary>A stub that answers nothing is a defect too, which the host reports as one. Sent through the
        /// registration's client directly, since only a stub other than <see cref="ControllerStub"/> answers so.</summary>
        [Test]
        public async Task AStubAnsweringNothing_IsReportedAsADefect()
        {
            Exception? failure = await RawCallFailure((_, _) => Task.FromResult<ControllerStubResponse>(null!));

            Assert.That((failure as ErrorWithCodeException)?.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_FAULTED));
        }

        /// <summary>A cancellation inside the stub is nobody's defect: the host ends the request, which the client
        /// sees as the connection ending.</summary>
        [Test]
        public async Task ACancellationInsideTheStub_EndsTheRequest_AsNoDefect()
        {
            Exception? failure = await RawCallFailure((_, _) => throw new OperationCanceledException("cancelled inside the stub"));

            Assert.That(failure, Is.TypeOf<HttpRequestException>());
        }

        private static async Task<Exception?> RawCallFailure(Func<ControllerStubRequest, CancellationToken, Task<ControllerStubResponse>> answer)
        {
            await using ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(new ScriptedStub(answer), CancellationToken.None);
            using HttpClient client = ControllerStubLoopback.CreateClient(host.BaseAddress, "http://s0.controllerstub.invalid");
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(host.BaseAddress, "ws/ControllerService"))
            {
                Content = new StringContent("<request/>"),
            };
            try
            {
                using HttpResponseMessage response = await client.SendAsync(request);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// The session cookie over both transports: a login presents none, later calls present the one it set, a
        /// disconnect ends it so the next call presents none, and a new login's cookie is the one presented after it.
        /// The two transports journal the same sequence, which pins that the loopback client keeps no cookie jar.
        /// </summary>
        [Test]
        public async Task TheSessionCookie_IsCarriedTheSameOverBothTransports()
        {
            IReadOnlyList<(string, string?, string?)> inProcess = await CookieRound(Transport.InProcess);
            IReadOnlyList<(string, string?, string?)> loopback = await CookieRound(Transport.Loopback);

            Assert.Multiple(() =>
            {
                Assert.That(inProcess.Select(call => call.Item2), Is.EqualTo(new[]
                {
                    null, "JSESSIONID=stub-1", "JSESSIONID=stub-1", null, null, "JSESSIONID=stub-2",
                }), "the cookie each call presented, in order");
                Assert.That(loopback, Is.EqualTo(inProcess));
            });
        }

        private static async Task<IReadOnlyList<(string, string?, string?)>> CookieRound(Transport transport)
        {
            var stub = new ControllerStub();
            var (sdk, host) = await Start(transport, stub);
            try
            {
                await sdk.Authentication.Authenticate();
                await sdk.Authentication.Ping();
                await sdk.Authentication.Disconnect();
                await sdk.Authentication.Ping();
                await sdk.Authentication.Authenticate();
                await sdk.Authentication.Ping();
                return [.. stub.Journal.All.Select(call => (call.SoapAction, call.Cookie, call.SetCookie))];
            }
            finally
            {
                await Stop(sdk, host);
            }
        }

        private static async Task<(StubbedSdk Sdk, ControllerStubLoopbackHost? Host)> Start(Transport transport, ControllerStub stub)
        {
            if (transport == Transport.InProcess)
            {
                return (StubbedSdk.Start(stub), null);
            }
            ControllerStubLoopbackHost host = await ControllerStubLoopbackHost.StartAsync(stub, CancellationToken.None);
            return (StubbedSdk.Start(stub, await ControllerStubs.RegisterLoopbackAsync(host.BaseAddress, CancellationToken.None)), host);
        }

        private static async Task Stop(StubbedSdk sdk, ControllerStubLoopbackHost? host)
        {
            sdk.Dispose();
            if (host is not null)
            {
                await host.DisposeAsync();
            }
        }

        /// <summary>A refusal at registration, stored in the registration's task, so the call that made the task has
        /// returned. It has sent the host nothing but the identity check, and says so.</summary>
        private static void AssertIdentityRefused(Task<ControllerStubRegistration> registering)
        {
            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => registering);
            Assert.Multiple(() =>
            {
                Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.CONTROLLER_STUB_IDENTITY_FAILED));
                Assert.That(refusal.Message, Does.EndWith(ControllerStubLoopback.NothingSent));
            });
        }

        // One header line per value, as two writers of the same header would put it on the wire.
        private static void WriteStubHeader(HttpContext context, string token, int times)
        {
            for (int i = 0; i < times; i++)
            {
                context.Response.Headers.Append(ControllerStubLoopback.IdentityHeader, token);
            }
        }

        private sealed class ScriptedStub(Func<ControllerStubRequest, CancellationToken, Task<ControllerStubResponse>> answer)
            : IControllerStub
        {
            public Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken) =>
                answer(request, cancellationToken);
        }
    }
}
