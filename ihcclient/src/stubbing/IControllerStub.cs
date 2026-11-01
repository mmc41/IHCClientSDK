using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.Stubbing
{
    /// <summary>
    /// Stands in for an IHC controller by answering one SOAP exchange at a time. Registered through
    /// <see cref="ControllerStubs.Register"/>, it receives every request a real SDK service sends to the
    /// registration's endpoint, after the SDK has built it, and its answer travels back through the SDK's own
    /// handler chain, so mapping, envelope guards, cookies and telemetry run as they do against a controller.
    /// </summary>
    /// <remarks>
    /// Plain values cross this contract rather than HTTP types, so an implementation can only answer an exchange;
    /// it can never be handed a transport that reaches a network.
    /// </remarks>
    public interface IControllerStub
    {
        /// <summary>Answers one exchange.</summary>
        /// <param name="request">The exchange the SDK sent.</param>
        /// <param name="cancellationToken">Cancelled when the caller abandons the call.</param>
        /// <returns>The controller's answer, as the SDK will read it.</returns>
        Task<ControllerStubResponse> HandleAsync(ControllerStubRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// One SOAP exchange as an SDK service sent it to a controller stub.
    /// </summary>
    /// <remarks>
    /// A sealed class rather than a record: an exchange message is read once by the stub, never compared, copied
    /// or deconstructed, so record equality and <c>with</c> would only add public surface.
    /// </remarks>
    /// <param name="service">The SOAP service the request is addressed to, such as <c>AuthenticationService</c>.</param>
    /// <param name="soapAction">The SOAP action the SDK posted, such as <c>authenticate</c>.</param>
    /// <param name="body">The SOAP envelope the SDK sent.</param>
    /// <param name="cookie">The session cookie the request presented, or null when it presented none.</param>
    public sealed class ControllerStubRequest(string service, string soapAction, string body, string? cookie)
    {
        /// <summary>The SOAP service the request is addressed to.</summary>
        public string Service { get; } = service;

        /// <summary>The SOAP action the SDK posted.</summary>
        public string SoapAction { get; } = soapAction;

        /// <summary>The SOAP envelope the SDK sent.</summary>
        public string Body { get; } = body;

        /// <summary>The session cookie the request presented, or null when it presented none.</summary>
        public string? Cookie { get; } = cookie;
    }

    /// <summary>
    /// A controller stub's answer to one exchange.
    /// </summary>
    /// <remarks>
    /// A sealed class rather than a record, for the reason <see cref="ControllerStubRequest"/> is one.
    /// </remarks>
    public sealed class ControllerStubResponse
    {
        private const int FirstFinalStatus = 200;
        private const int LastFinalStatus = 599;

        /// <summary>
        /// Builds an answer, refusing one that no final HTTP response carries. A stub that builds one then fails
        /// where it built it, which the SDK reports as a stub defect; carried on, it would reach the caller as a
        /// controller's status or a transport failure, and differently over the two transports.
        /// </summary>
        /// <param name="statusCode">The HTTP status the controller answers with, a final status from 200 to 599.</param>
        /// <param name="body">The SOAP envelope the controller answers with.</param>
        /// <param name="setCookie">A session cookie the answer sets, or null when it sets none.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> is no final status.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
        public ControllerStubResponse(int statusCode, string body, string? setCookie = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, FirstFinalStatus);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(statusCode, LastFinalStatus);
            ArgumentNullException.ThrowIfNull(body);
            StatusCode = statusCode;
            Body = body;
            SetCookie = setCookie;
        }

        private ControllerStubResponse()
        {
            Body = string.Empty;
            IsConnectionLost = true;
        }

        /// <summary>
        /// A deliberate transport failure: the connection is lost before any answer arrives. Only the exception
        /// type the SDK raises for it is promised, never a finer reason.
        /// </summary>
        public static ControllerStubResponse ConnectionLost { get; } = new();

        /// <summary>The HTTP status the controller answers with.</summary>
        public int StatusCode { get; }

        /// <summary>The SOAP envelope the controller answers with.</summary>
        public string Body { get; }

        /// <summary>A session cookie the answer sets, or null when it sets none.</summary>
        public string? SetCookie { get; }

        /// <summary>True for <see cref="ConnectionLost"/>, the answer that is no answer.</summary>
        public bool IsConnectionLost { get; }
    }
}
