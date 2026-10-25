using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ihc.Envelope;
using NUnit.Framework;
using Api = Ihc.Soap.Openapi;
using Auth = Ihc.Soap.Authentication;

namespace Ihc.Tests
{
    /// <summary>
    /// What a login puts on the wire, and what it does to the cookie already stored, for both services that
    /// log in.
    ///
    /// A login opens a session, so it presents no cookie - and the case that matters is a RE-login, made while
    /// a cookie is still stored: after a controller restart that cookie names a session the controller no
    /// longer has. The stored cookie is withheld rather than cleared, so what replaces it is still the login's
    /// own answer - the new cookie when the controller accepts, nothing when it refuses - and an attempt that
    /// gets no answer it can decode leaves a session that may still be live exactly as it was.
    ///
    /// Driven over the real handler chain with a stub socket: the subject is the request's headers, which a
    /// faked SOAP layer never builds.
    /// </summary>
    [TestFixture]
    public class LoginCookieTests
    {
        private const string CookieA = "JSESSIONID=A1A1A1A1A1A1";
        private const string CookieB = "JSESSIONID=B2B2B2B2B2B2";

        /// <summary>
        /// How the controller answers the second login. Every answer that has headers offers
        /// <see cref="CookieB"/>, so the cases show which answers may install a cookie rather than relying on
        /// the others never carrying one.
        /// </summary>
        public enum Answer
        {
            Accepted,
            Refused,

            /// <summary>The connection fails before any reply arrives.</summary>
            NoReply,

            ServerError,

            /// <summary>A 200 whose body is not a SOAP envelope.</summary>
            Undecodable,
        }

        private static IEnumerable<LoginService> Services() => [LoginService.Authentication, LoginService.OpenApi];

        /// <summary>
        /// The change itself, over every answer the controller can give: the second login goes out with no
        /// Cookie header, in neither header collection, although the first login's cookie is still STORED as it
        /// goes - withheld, not cleared. The request is built before any answer exists, so how the controller
        /// then answers cannot matter to it.
        /// </summary>
        [Test]
        public async Task SecondLogin_SendsNoCookie_ThoughTheFirstIsStillStored(
            [ValueSource(nameof(Services))] LoginService service, [Values] Answer answer)
        {
            using var rig = new Rig(service, answer);

            // Whether the login then succeeds is the subject of the cases below, not of this one.
            _ = await rig.ReloginAsync();

            Assert.Multiple(() =>
            {
                Assert.That(rig.SecondLogin.RequestHeaders, Does.Not.ContainKey("Cookie"));
                Assert.That(rig.SecondLogin.ContentHeaders, Does.Not.ContainKey("Cookie"),
                    "nor smuggled through the content headers");
                Assert.That(rig.StoredDuringSecondLogin, Is.EqualTo(CookieA),
                    "the first session's cookie is still stored while the login is on the wire");
            });
        }

        /// <summary>An accepted re-login replaces the stored cookie with its own, and the call after it rides the
        /// session it opened.</summary>
        [Test]
        public async Task SecondLogin_Accepted_InstallsItsCookie_WhichTheNextCallPresents(
            [ValueSource(nameof(Services))] LoginService service)
        {
            using var rig = new Rig(service, Answer.Accepted);

            Exception? thrown = await rig.ReloginAsync();
            await rig.CallAsync();

            Assert.Multiple(() =>
            {
                Assert.That(thrown, Is.Null, "the controller accepted the login");
                Assert.That(rig.StoredCookie, Is.EqualTo(CookieB));
                Assert.That(rig.LastRequest.RequestHeaders, Does.ContainKey("Cookie").WithValue(CookieB));
            });
        }

        /// <summary>
        /// A re-login that gets no answer it can decode - no reply at all, a non-success status, or a body that is
        /// not SOAP - changes nothing: the first session's cookie is still stored and the next call still presents
        /// it, although each of those answers that had headers offered another. That is what withholding the
        /// cookie rather than clearing it buys: an attempt that fails on the way does not end a session that may
        /// still be live.
        /// </summary>
        [Test]
        public async Task SecondLogin_WithNoDecodableAnswer_KeepsTheStoredCookie_WhichTheNextCallPresents(
            [ValueSource(nameof(Services))] LoginService service,
            [Values(Answer.NoReply, Answer.ServerError, Answer.Undecodable)] Answer answer)
        {
            using var rig = new Rig(service, answer);

            Exception? thrown = await rig.ReloginAsync();
            await rig.CallAsync();

            Assert.Multiple(() =>
            {
                Assert.That(thrown, Is.Not.Null, "a login with no decodable answer fails");
                Assert.That(rig.StoredCookie, Is.EqualTo(CookieA));
                Assert.That(rig.LastRequest.RequestHeaders, Does.ContainKey("Cookie").WithValue(CookieA));
            });
        }

        /// <summary>
        /// A re-login the controller REFUSES clears the stored cookie - the cookie offered with the refusal is not
        /// installed either - and the next call goes out with no Cookie header at all.
        /// </summary>
        [Test]
        public async Task SecondLogin_Refused_ClearsTheStoredCookie_ThoughItsAnswerSetsOne(
            [ValueSource(nameof(Services))] LoginService service)
        {
            using var rig = new Rig(service, Answer.Refused);

            Exception? thrown = await rig.ReloginAsync();
            await rig.CallAsync();

            Assert.Multiple(() =>
            {
                Assert.That(thrown, Is.Not.Null, "the controller refused the login");
                Assert.That(rig.StoredCookie, Is.Null);
                Assert.That(rig.LastRequest.RequestHeaders, Does.Not.ContainKey("Cookie"));
            });
        }

        /// <summary>A SOAP answer carrying <paramref name="body"/>, written by the SDK's own serializer.</summary>
        private static string Envelope<T>(T body) => Serialization.SerializeXml(new ResponseEnvelope<T>(body));

        /// <summary>An HTTP answer with <paramref name="body"/>, offering <paramref name="setCookie"/> when there is one.</summary>
        private static HttpResponseMessage Answering(HttpStatusCode status, string body, string? setCookie)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/xml")
            };
            if (setCookie is not null)
            {
                response.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
            }
            return response;
        }

        private static HttpResponseMessage SecondLoginAnswer(Answer answer, LoginService service) => answer switch
        {
            Answer.Accepted => Answering(HttpStatusCode.OK, service.LoginAnswer(accepted: true), CookieB),
            Answer.Refused => Answering(HttpStatusCode.OK, service.LoginAnswer(accepted: false), CookieB),
            Answer.NoReply => throw new HttpRequestException("the connection closed before any reply"),
            Answer.ServerError => Answering(HttpStatusCode.InternalServerError, "<html>boom</html>", CookieB),
            Answer.Undecodable => Answering(HttpStatusCode.OK, "<html>not a SOAP envelope</html>", CookieB),
            _ => throw new ArgumentOutOfRangeException(nameof(answer), answer, null),
        };

        /// <summary>A service built over a socket, reduced to what these cases do with it.</summary>
        internal sealed record Session(ICookieHandlerService Service, Func<Task> Login, Func<Task> Call);

        /// <summary>
        /// One of the two services that log in, as these cases drive it - how it is built over a socket, logged
        /// in and called, and how its controller words the answers. Public only because an NUnit case parameter
        /// travels through a public signature.
        /// </summary>
        public sealed class LoginService
        {
            internal static readonly LoginService Authentication = new(
                nameof(AuthenticationService),
                (settings, transport) =>
                {
                    var service = new AuthenticationService(settings, transport);
                    return new Session(service, () => service.Authenticate(), () => service.Ping());
                },
                accepted => Envelope(new Auth.outputMessageName2(accepted
                    ? new Auth.WSLoginResult { loginWasSuccessful = true, loggedInUser = new Auth.WSUser { username = "installer" } }
                    : new Auth.WSLoginResult { loginWasSuccessful = false })),
                new Dictionary<string, string>
                {
                    ["ping"] = Envelope(new Auth.outputMessageName3(true)),
                    // Disposing a session that is still connected logs it out.
                    ["disconnect"] = Envelope(new Auth.outputMessageName1(true)),
                });

            internal static readonly LoginService OpenApi = new(
                nameof(OpenAPIService),
                (settings, transport) =>
                {
                    var service = new OpenAPIService(settings, transport);
                    return new Session(service, () => service.Authenticate(), () => service.Ping());
                },
                accepted => Envelope(new Api.outputMessageName13(accepted)),
                new Dictionary<string, string> { ["ping"] = Envelope(new Api.outputMessageName10()) });

            private readonly string name;
            private readonly Func<IhcSettings, HttpClient, Session> open;
            private readonly Func<bool, string> loginAnswer;
            private readonly IReadOnlyDictionary<string, string> callAnswers;

            private LoginService(string name, Func<IhcSettings, HttpClient, Session> open,
                Func<bool, string> loginAnswer, IReadOnlyDictionary<string, string> callAnswers)
            {
                this.name = name;
                this.open = open;
                this.loginAnswer = loginAnswer;
                this.callAnswers = callAnswers;
            }

            internal Session Open(IhcSettings settings, HttpClient transport) => open(settings, transport);

            internal string LoginAnswer(bool accepted) => loginAnswer(accepted);

            internal HttpResponseMessage AnswerCall(string action) =>
                Answering(HttpStatusCode.OK, callAnswers[action], setCookie: null);

            public override string ToString() => name;
        }

        /// <summary>
        /// One service over a stub socket scripted for a re-login: the first login is accepted with
        /// <see cref="CookieA"/>, the second answered as the case says, and any other call answered normally.
        /// It also records what the service had STORED as each login reached the socket - the one fact the
        /// socket cannot read off the request itself.
        /// </summary>
        private sealed class Rig : IDisposable
        {
            private const string LoginAction = "authenticate";

            private readonly List<string?> storedDuringLogins = new();
            private readonly HttpClient transport;
            private readonly Session session;

            internal Rig(LoginService service, Answer second)
            {
                Socket = new StubTransport(request =>
                {
                    string action = request.Headers.GetValues("SOAPAction").Single();
                    if (action != LoginAction)
                    {
                        return service.AnswerCall(action);
                    }
                    storedDuringLogins.Add(StoredCookie);
                    return storedDuringLogins.Count switch
                    {
                        1 => Answering(HttpStatusCode.OK, service.LoginAnswer(accepted: true), CookieA),
                        2 => SecondLoginAnswer(second, service),
                        _ => throw new InvalidOperationException("this rig scripts two logins"),
                    };
                });
                transport = Client.CreateHttpClient(Socket);
                session = service.Open(FakeSession.Settings(), transport);
            }

            internal StubTransport Socket { get; }

            internal string? StoredCookie => session.Service.GetCookieHandler().GetCookie();

            /// <summary>The second login's request, as it reached the socket.</summary>
            internal StubTransport.SentRequest SecondLogin =>
                Socket.Requests.Where(r => r.SoapAction == LoginAction).ElementAt(1);

            /// <summary>What the service had stored while the second login was on the wire.</summary>
            internal string? StoredDuringSecondLogin => storedDuringLogins[1];

            internal StubTransport.SentRequest LastRequest => Socket.Requests[^1];

            /// <summary>
            /// Logs in twice and hands back what the second login threw - the cases differ in whether it should.
            /// Asserts on the way what every case starts from, that the first login stored its answer's cookie:
            /// without it, a case asserting the cookie SURVIVED would assert over a session that never began.
            /// </summary>
            internal async Task<Exception?> ReloginAsync()
            {
                await session.Login();
                Assert.That(StoredCookie, Is.EqualTo(CookieA), "the first login must store the cookie its answer set");

                try
                {
                    await session.Login();
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }

            /// <summary>An ordinary call: the one the next request after the logins is.</summary>
            internal Task CallAsync() => session.Call();

            public void Dispose()
            {
                (session.Service as IDisposable)?.Dispose();
                transport.Dispose();
            }
        }
    }
}
