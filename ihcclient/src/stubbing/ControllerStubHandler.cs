using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The innermost handler of an in-process registration's client: it turns the request the SDK built into a
    /// <see cref="ControllerStubRequest"/>, and the stub's answer back into an HTTP response. It sits under the
    /// SDK's own logging handler, so a stubbed call is traced exactly as a real one is.
    /// </summary>
    internal sealed class ControllerStubHandler(IControllerStub stub) : HttpMessageHandler
    {
        private const string SoapActionHeader = "SOAPAction";
        private const string CookieHeader = "Cookie";
        private const string SetCookieHeaderName = "Set-Cookie";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var exchange = new ControllerStubRequest(
                ServiceOf(request.RequestUri),
                HeaderOf(request, SoapActionHeader) ?? string.Empty,
                body,
                HeaderOf(request, CookieHeader));

            ControllerStubResponse? answer;
            try
            {
                answer = await stub.HandleAsync(exchange, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A cancellation is the caller's or the client's, never the stub's fault, so it stays one.
                throw;
            }
            catch (Exception ex)
            {
                // A stub defect is kept apart from every deliberate failure: turning it into a SOAP fault would make
                // it look like an answer the stub meant to give.
                throw Faulted("threw " + ex.GetType().FullName, ex);
            }

            if (answer is null)
            {
                throw Faulted("returned no response", null);
            }
            if (answer.IsConnectionLost)
            {
                throw new HttpRequestException("The controller stub dropped the connection before answering.",
                    new IOException("Connection lost (controller stub)."));
            }

            var response = new HttpResponseMessage((HttpStatusCode)answer.StatusCode)
            {
                Content = new StringContent(answer.Body, Encoding.UTF8, "text/xml"),
                RequestMessage = request,
            };
            if (answer.SetCookie is not null)
            {
                // Without validation, as a controller's own header arrives: the SDK reads the raw value back.
                response.Headers.TryAddWithoutValidation(SetCookieHeaderName, answer.SetCookie);
            }
            return response;
        }

        /// <summary>
        /// The refusal for a stub defect. It names the defect by kind and exception type only: the message of an
        /// exception thrown inside a stub is not read here, because the stub may not be able to produce one.
        /// </summary>
        private ErrorWithCodeException Faulted(string what, Exception? inner)
        {
            string message = $"{nameof(Errors.CONTROLLER_STUB_FAULTED)}: the controller stub {stub.GetType().FullName} "
                + what + ", which is a defect in the stub rather than a controller's answer.";
            return inner is null
                ? new ErrorWithCodeException(Errors.CONTROLLER_STUB_FAULTED, message)
                : new ErrorWithCodeException(Errors.CONTROLLER_STUB_FAULTED, message, inner);
        }

        /// <summary>The last segment of the request path, which is the SOAP service a service URL addresses.</summary>
        private static string ServiceOf(Uri? requestUri)
        {
            string path = requestUri?.AbsolutePath ?? string.Empty;
            return path[(path.LastIndexOf('/') + 1)..];
        }

        private static string? HeaderOf(HttpRequestMessage request, string name) =>
            request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
    }
}
