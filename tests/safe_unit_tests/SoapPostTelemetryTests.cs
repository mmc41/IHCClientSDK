using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Envelope;
using Ihc.Soap.Controller;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What a SOAP call publishes to telemetry.
    ///
    /// A SOAP envelope is unbounded and carries whatever the controller was asked about, so exporting it
    /// wholesale puts arbitrary installation data - and anything the redactor does not recognise - into the
    /// trace backend. The span therefore carries bounded metadata always, and the envelopes themselves only
    /// when <see cref="IhcSettings.LogSensitiveData"/> says so. The flag is the trust boundary and it
    /// defaults to false, so the default-configuration cases below are the ones that matter.
    ///
    /// The response direction is pinned as hard as the request direction because it is where the gap was:
    /// the response envelope used to be exported unconditionally, ignoring the flag entirely.
    /// </summary>
    [TestFixture]
    public class SoapPostTelemetryTests
    {
        /// <summary>Stands in for the socket: records what was sent, answers with a canned response.</summary>
        private sealed class StubTransport : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly string responseBody;

            public StubTransport(HttpStatusCode status, string responseBody)
            {
                this.status = status;
                this.responseBody = responseBody;
            }

            public string? RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "text/xml")
                };
            }
        }

        private sealed class TestSoapService : ServiceBaseImpl
        {
            public TestSoapService(IhcSettings settings, ICookieHandler cookieHandler, HttpClient transport)
                : base(cookieHandler, settings, "TestService", transport) { }

            public Task<outputMessageName9> Call() =>
                soapPost<outputMessageName9, inputMessageName9>("isSDCardReady", new inputMessageName9());
        }

        private static IhcSettings Settings(bool logSensitiveData) =>
            new IhcSettings
            {
                Endpoint = "http://unit.test.local",
                AsyncContinueOnCapturedContext = false,
                LogSensitiveData = logSensitiveData
            };

        private static string SoapResponse(bool value) =>
            Serialization.SerializeXml<ResponseEnvelope<outputMessageName9>>(
                new ResponseEnvelope<outputMessageName9>(new outputMessageName9(value)));

        /// <summary>Runs one call and returns every tag the soapPost span carried, plus what was actually sent.</summary>
        private static async Task<(Dictionary<string, object?> Tags, string? RequestBody, Exception? Thrown)>
            CallCapturingSpan(bool logSensitiveData, HttpStatusCode status = HttpStatusCode.OK)
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { "soapPost.isSDCardReady" });

            var stub = new StubTransport(status, SoapResponse(true));
            using var transport = Client.CreateHttpClient(stub);
            var service = new TestSoapService(Settings(logSensitiveData), new CookieHandler(false), transport);

            Exception? thrown = null;
            try
            {
                await service.Call();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            var tags = new Dictionary<string, object?>();
            foreach (Activity span in capture.Spans)
            {
                foreach (KeyValuePair<string, object?> tag in span.TagObjects)
                {
                    tags[tag.Key] = tag.Value;
                }
            }
            return (tags, stub.RequestBody, thrown);
        }

        private const string RequestPayloadTag = Telemetry.argsTagPrefix + "request";

        /// <summary>
        /// The controller-duration histogram, recorded at the execute-around every SOAP call passes through.
        /// It cannot live on the StartActivity helper: that returns a bare Activity and its caller owns the
        /// using, so the helper is never present when the operation ends.
        /// </summary>
        [Test]
        public async Task SoapPost_RecordsExactlyOneControllerDurationPoint_WithItsClosedDimensions()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                instruments: new[] { "ihc.controller.operation.duration" });

            var stub = new StubTransport(HttpStatusCode.OK, SoapResponse(true));
            using var transport = Client.CreateHttpClient(stub);
            await new TestSoapService(Settings(false), new CookieHandler(false), transport).Call();

            IReadOnlyList<CapturedPoint> recorded = capture.PointsOf("ihc.controller.operation.duration");

            Assert.Multiple(() =>
            {
                Assert.That(recorded, Has.Count.EqualTo(1), "one controller call is one point, never zero or two");
                Assert.That(recorded[0].Value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(recorded[0].Tag("ihc.service"), Is.EqualTo("TestService"));
                Assert.That(recorded[0].Tag("ihc.operation"), Is.EqualTo("isSDCardReady"));
                Assert.That(recorded[0].Tags, Does.Not.ContainKey("error.type"),
                    "a successful call carries no error dimension at all, so the success series stays one series");
                // The dimension set is CLOSED: an unlisted dimension would multiply every series silently.
                Assert.That(recorded[0].Tags.Keys,
                    Is.EquivalentTo(new[] { "ihc.edit.status", "ihc.service", "ihc.operation" }));
            });
        }

        [Test]
        public async Task SoapPost_WhenTheCallFails_RecordsThePointWithTheNormalizedErrorType()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                instruments: new[] { "ihc.controller.operation.duration" });

            var stub = new StubTransport(HttpStatusCode.InternalServerError, SoapResponse(true));
            using var transport = Client.CreateHttpClient(stub);
            Assert.ThrowsAsync<HttpRequestException>(async () =>
                await new TestSoapService(Settings(false), new CookieHandler(false), transport).Call());

            IReadOnlyList<CapturedPoint> recorded = capture.PointsOf("ihc.controller.operation.duration");

            Assert.Multiple(() =>
            {
                Assert.That(recorded, Has.Count.EqualTo(1), "a failed call is still timed");
                Assert.That(recorded[0].Tag("ihc.edit.status"), Is.EqualTo("failed"));
                Assert.That(recorded[0].Tag("error.type"), Is.EqualTo("System.Net.Http.HttpRequestException"));
            });
        }

        [Test]
        public async Task SoapPost_WithoutSensitiveLogging_ExportsNeitherEnvelope()
        {
            var (tags, _, thrown) = await CallCapturingSpan(logSensitiveData: false);

            Assert.That(thrown, Is.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tags, Does.Not.ContainKey(RequestPayloadTag),
                    "the request envelope must not leave the process under default settings");
                Assert.That(tags, Does.Not.ContainKey(Telemetry.returnValueTag),
                    "the response envelope must not leave the process under default settings - this is the direction that used to ignore the flag");
            });
        }

        [Test]
        public async Task SoapPost_WithSensitiveLogging_ExportsBothEnvelopes()
        {
            var (tags, _, thrown) = await CallCapturingSpan(logSensitiveData: true);

            Assert.That(thrown, Is.Null);
            Assert.Multiple(() =>
            {
                Assert.That(tags, Does.ContainKey(RequestPayloadTag));
                Assert.That(tags[RequestPayloadTag]?.ToString(), Does.Contain("Envelope"));
                Assert.That(tags, Does.ContainKey(Telemetry.returnValueTag));
                Assert.That(tags[Telemetry.returnValueTag]?.ToString(), Does.Contain("Envelope"));
            });
        }

        /// <summary>The bounded replacement: present either way, and sized in wire bytes rather than characters.</summary>
        [Test]
        public async Task SoapPost_RecordsBoundedMetadataRegardlessOfTheFlag([Values(false, true)] bool logSensitiveData)
        {
            var (tags, requestBody, _) = await CallCapturingSpan(logSensitiveData);

            Assert.Multiple(() =>
            {
                Assert.That(tags[Telemetry.argsTagPrefix + "soapAction"], Is.EqualTo("isSDCardReady"));
                Assert.That(tags[SdkTelemetryRegistry.Attributes.SoapRequestBodySize],
                    Is.EqualTo(Encoding.UTF8.GetByteCount(requestBody!)));
                Assert.That(tags[SdkTelemetryRegistry.Attributes.SoapResponseBodySize],
                    Is.EqualTo(Encoding.UTF8.GetByteCount(SoapResponse(true))));
                Assert.That(tags[SdkTelemetryRegistry.Attributes.OperationStatus], Is.EqualTo(SdkTelemetryRegistry.Values.StatusOk));
            });
        }

        /// <summary>
        /// A failing call must still say what happened, and must still not leak the request envelope -
        /// the error path is exactly where the temptation to dump the payload is strongest.
        /// </summary>
        [Test]
        public async Task SoapPost_FailedCall_RecordsErrorOutcomeAndStillWithholdsThePayload()
        {
            var (tags, _, thrown) = await CallCapturingSpan(logSensitiveData: false, status: HttpStatusCode.InternalServerError);

            Assert.That(thrown, Is.InstanceOf<HttpRequestException>());
            Assert.Multiple(() =>
            {
                Assert.That(tags[SdkTelemetryRegistry.Attributes.OperationStatus], Is.EqualTo(SdkTelemetryRegistry.Values.StatusFailed));
                Assert.That(tags, Does.Not.ContainKey(RequestPayloadTag));
                Assert.That(tags, Does.Not.ContainKey(Telemetry.returnValueTag));
                Assert.That(tags[SdkTelemetryRegistry.Attributes.SoapRequestBodySize], Is.Not.Null,
                    "the request size is known before the call fails, so it must still be reported");
            });
        }
    }
}
