using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ihc.Soap.Controller;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What a SOAP response that deserializes to NOTHING must produce.
    ///
    /// <c>soapPost</c> refuses a null envelope, then returns <c>respObj.Body</c> unchecked - and
    /// <see cref="Ihc.Envelope.ResponseEnvelope{T}"/> declares <c>Body</c> as <c>default!</c>, which for
    /// every generated message type is null. What each service then did with that null varied: most raised a
    /// bare <see cref="System.NullReferenceException"/> carrying no cause, and
    /// <see cref="ControllerService.StoreProject"/> folded it to a clean <c>false</c> AFTER sending the
    /// project bytes. One guard on the shared path gives all of them one answer.
    ///
    /// The reachable shapes are MEASURED here rather than assumed, because the obvious candidate is not one
    /// of them: an <c>&lt;Envelope&gt;&lt;Body/&gt;&lt;/Envelope&gt;</c> deserializes to a message object with
    /// every field defaulted, and so does a SOAP <c>&lt;Fault&gt;</c>. Only an envelope carrying no Body
    /// ELEMENT leaves <c>Body</c> null. The defaulted-object shape is the one the per-call guards answer -
    /// <see cref="ControllerServiceStoreProjectTests.StoreProject_AbsentAcknowledgement_IsRefusedRatherThanReportedAsADeclinedStore"/>
    /// is its case - and the two together cover what a response that says nothing can look like.
    /// </summary>
    [TestFixture]
    public class SoapEnvelopeGuardTests
    {
        private static TestSoapService ServiceAnswering(string responseXml)
        {
            var content = new StringContent(responseXml, Encoding.UTF8, "text/xml");
            var transport = Client.CreateHttpClient(new StubTransport(HttpStatusCode.OK, content));
            return new TestSoapService(FakeSession.Settings(), new CookieHandler(false), transport);
        }

        private const string NoBodyElement =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Envelope xmlns=\"http://schemas.xmlsoap.org/soap/envelope/\"></Envelope>";

        private const string HeaderOnly =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Envelope xmlns=\"http://schemas.xmlsoap.org/soap/envelope/\"><Header/></Envelope>";

        [TestCase(NoBodyElement, TestName = "{m}(an envelope carrying no Body element)")]
        [TestCase(HeaderOnly, TestName = "{m}(an envelope carrying only a Header)")]
        public void AResponseWhoseBodyTheDeserializerCannotMatch_IsRefusedWithACode(string responseXml)
        {
            var service = ServiceAnswering(responseXml);

            var ex = Assert.ThrowsAsync<ErrorWithCodeException>(async () => await service.Call());

            Assert.Multiple(() =>
            {
                Assert.That(ex!.ErrorCode, Is.EqualTo(Errors.XML_DESERIALIZE_ERROR));
                Assert.That(ex.Message, Does.Contain("isSDCardReady"),
                    "the refusal must name the call whose response could not be read");
            });
        }

        /// <summary>A well-formed response still reads, so the guard is proven to add a case rather than
        /// to sit on the success path.</summary>
        [Test]
        public async Task AWellFormedResponse_StillReads()
        {
            var service = ServiceAnswering(TestSoapService.Response(true));

            outputMessageName9 result = await service.Call();

            Assert.That(result.isSDCardReady1, Is.True);
        }
    }
}
