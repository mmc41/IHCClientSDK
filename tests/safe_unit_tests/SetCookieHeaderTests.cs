using System.Net;
using System.Net.Http;
using Ihc;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Reading the session cookie off an authentication response. The controller is not obliged to send
    /// <c>Set-Cookie</c> on every 200, so an absent header is an ordinary outcome the caller already handles
    /// (it sets a null cookie) — it must not surface as an exception thrown from inside a response callback.
    /// </summary>
    [TestFixture]
    public class SetCookieHeaderTests
    {
        private static HttpResponseMessage Response(params string[] setCookieValues)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            foreach (string value in setCookieValues)
            {
                response.Headers.Add("Set-Cookie", value);
            }
            return response;
        }

        /// <summary>The defect: an absent Set-Cookie threw InvalidOperationException instead of yielding null.</summary>
        [Test]
        public void FirstOrNull_NoSetCookieHeader_IsNullRatherThanThrowing()
        {
            using HttpResponseMessage response = Response();

            Assert.That(SetCookieHeader.FirstOrNull(response), Is.Null);
        }

        [Test]
        public void FirstOrNull_SingleSetCookieHeader_ReturnsIt()
        {
            using HttpResponseMessage response = Response("JSESSIONID=abc123; Path=/");

            Assert.That(SetCookieHeader.FirstOrNull(response), Is.EqualTo("JSESSIONID=abc123; Path=/"));
        }

        /// <summary>A response may carry several cookies; the session cookie is the first, as before.</summary>
        [Test]
        public void FirstOrNull_MultipleSetCookieHeaders_ReturnsTheFirst()
        {
            using HttpResponseMessage response = Response("JSESSIONID=abc123; Path=/", "other=zzz");

            Assert.That(SetCookieHeader.FirstOrNull(response), Is.EqualTo("JSESSIONID=abc123; Path=/"));
        }
    }
}
