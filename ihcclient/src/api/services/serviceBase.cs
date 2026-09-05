using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace Ihc
{
    /// <summary>
    /// Base interface for all IHC services.
    /// </summary>
    public interface IIHCApiService
    {
        /// <summary>
        /// The IhcSettings used by this service.
        /// </summary>
        public IhcSettings IhcSettings { get; }
    }
    
    /// <summary>
    /// Base class for both high level IHC Api services.
    /// </summary>
    public abstract class ServiceBase
    {
        protected readonly IhcSettings settings;

        // One entry point per service instance, named for the concrete type so spans keep reading
        // "<wrapper>.<operation>". The naming is the core's; see StartActivity below for what this helper
        // structurally cannot delegate.
        private readonly OperationTelemetry telemetry;

        /// <summary>
        /// The settings carried by <paramref name="authService"/>, refusing a null session first.
        /// </summary>
        /// <remarks>
        /// Every authenticated service reaches this base through this helper rather than through
        /// <c>authService.IhcSettings</c> directly, because a base initializer runs before any statement the
        /// deriving constructor could hold a guard in. Without it a null session dereferences there, and a
        /// caller compiled without nullable reference types — the caller CA1062 exists for — gets a
        /// NullReferenceException raised inside the SDK instead of an argument refusal naming the parameter.
        /// </remarks>
        private protected static IhcSettings SettingsOf([NotNull] IAuthenticationService? authService)
        {
            ArgumentNullException.ThrowIfNull(authService);
            return authService.IhcSettings;
        }

        protected ServiceBase(IhcSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            this.telemetry = new OperationTelemetry(SdkTelemetryRegistry.Surface, this.GetType().Name);
            this.settings = settings;

            if (this.settings.Endpoint == null)
            {
                throw new ArgumentException("IhcSettings field Endpoint must be supplied");
            }

            if (this.settings.Endpoint.StartsWith(SpecialEndpoints.MockedPrefix, StringComparison.Ordinal))
            {
                throw new ArgumentException("IhcSettings specifies a mocked implmentation which does not correspond to this real implemenentation");
            }
        }

        public IhcSettings IhcSettings
        {
            get { return settings; }
        }

        /// <summary>
        /// Starts a span named <c>&lt;wrapper&gt;.&lt;operation&gt;</c>.
        /// </summary>
        /// <remarks>
        /// SPAN CONCERNS ONLY, and that is a limit rather than a choice. The helper returns a bare
        /// <see cref="Activity"/> by its shipped signature, so its caller owns the <c>using</c> and the helper
        /// is not present when the operation ENDS - which is the only moment a duration or an outcome could
        /// be recorded. A metric therefore cannot live here; the controller-duration histogram is recorded at
        /// the execute-around every SOAP call already passes through instead.
        /// <para>The error-type policy reaches this tier's call sites without any of them changing, because
        /// they all report failure through <see cref="ActivityExtensions.SetError"/>.</para>
        /// </remarks>
        protected Activity? StartActivity(string operationName) => telemetry.StartSpan(operationName);
    }

    /// <summary>
    /// Callback interface for doing side effects as part of a SOAP HTTP POST.
    /// </summary>
    /// <param name="msg">The HTTP response message.</param>
    public delegate void OnOkCallBack(HttpResponseMessage msg);

    /// <summary>
    /// Common base class for low level service implementations of IHC SOAP interfaces.
    /// </summary>
    internal abstract class ServiceBaseImpl
    {
        protected readonly Client ihcClient;
        protected readonly ICookieHandler cookieHandler;
        public readonly string Url;
        protected IhcSettings settings;


        /// <summary>The SOAP service this implementation wraps, recorded as the histogram's service dimension.</summary>
        private readonly string serviceName;

        // Owner "soapPost" rather than the wrapping type: it keeps the span's existing
        // "soapPost.<action>" name, which is what every existing query addresses these calls by.
        private readonly OperationTelemetry telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(soapPost));

        /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
        private static readonly MetricBinding ControllerCallMetrics =
            MetricBinding.For(SdkTelemetryRegistry.ControllerOperationDuration);

        /// <param name="cookieHandler">Session cookie source.</param>
        /// <param name="settings">IHC settings.</param>
        /// <param name="serviceName">Name of the IHC SOAP service this implementation wraps.</param>
        /// <param name="transport">
        /// Transport to use instead of the process-wide singleton HttpClient. Null in production; the seam
        /// unit tests substitute a stub transport through (they own it and dispose it).
        /// </param>
        protected ServiceBaseImpl(ICookieHandler cookieHandler, IhcSettings settings, string serviceName, HttpClient? transport = null)
        {
            this.settings = settings;
            this.serviceName = serviceName;
            this.Url = UrlOf(settings, serviceName);
            this.cookieHandler = cookieHandler;
            this.ihcClient = new Client(cookieHandler, Url, settings, transport);
        }

        /// <summary>
        /// The address a named SOAP service is reached at. Exposed so a wrapper that names the endpoint
        /// in a diagnostic can build it from its settings, rather than reaching through the
        /// implementation it holds - which, for a wrapper carrying a test seam, is not necessarily one
        /// of these at all.
        /// </summary>
        internal static string UrlOf(IhcSettings settings, string serviceName) =>
            settings.Endpoint + "/ws/" + serviceName;

        private static string escapeXMl(string xmlString)
        {
            return System.Security.SecurityElement.Escape(xmlString);
        }

        /// <summary>
        /// Performs a SOAP HTTP POST action.
        /// </summary>
        /// <typeparam name="RESP">Response type.</typeparam>
        /// <typeparam name="REQ">Request type.</typeparam>
        /// <param name="soapAction">SOAP action name.</param>
        /// <param name="request">Request object.</param>
        /// <param name="onOkSideEffect">Optional callback for side effects on success.</param>
        /// <returns>The response object.</returns>
        protected Task<RESP> soapPost<RESP, REQ>(string soapAction, REQ request, OnOkCallBack? onOkSideEffect = null) =>
            // The execute-around EVERY controller SOAP call passes through, which is why the duration
            // histogram lives here rather than on the StartActivity helper: that helper hands back a bare
            // Activity and its caller owns the using, so it is never present when the call ends.
            telemetry.RunAsync(soapAction, async scope =>
            {
                scope.AddMetricTag(SdkTelemetryRegistry.Attributes.Service, serviceName);
                scope.AddMetricTag(SdkTelemetryRegistry.Attributes.Operation, soapAction);
                return await SoapPostBody<RESP, REQ>(scope.Activity, soapAction, request, onOkSideEffect)
                    .ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }, ControllerCallMetrics);

        private async Task<RESP> SoapPostBody<RESP, REQ>(Activity? activity, string soapAction, REQ request, OnOkCallBack? onOkSideEffect)
        {
            var req = Serialization.SerializeXml<RequestEnvelope<REQ>>(new RequestEnvelope<REQ>(request));

            activity.SetParameters(
                (nameof(soapAction), soapAction),
                (nameof(onOkSideEffect), onOkSideEffect != null)
            );
            // A SOAP envelope is unbounded and carries whatever the controller was asked about, so its
            // size is exported always and its content only on request. The redactor knows about
            // passwords; it cannot know about the rest of an installation's data.
            activity?.SetTag(SdkTelemetryRegistry.Attributes.SoapRequestBodySize, System.Text.Encoding.UTF8.GetByteCount(req));
            if (settings.LogSensitiveData)
            {
                activity.SetParameters((nameof(request), escapeXMl(req)));
            }

            // The response is ours to dispose: HttpClient disposes the response it hands back only on its own
            // failure path (HttpClient.HandleFailure), and EnsureSuccessStatusCode below does not dispose the
            // content either - so a non-2xx answer would otherwise walk out of here with the response still open.
            using var httpResp = await ihcClient.Post(soapAction, req).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            httpResp.EnsureSuccessStatusCode();

            if (onOkSideEffect != null)
            {
                onOkSideEffect(httpResp);
            }

            string respStr = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            activity?.SetTag(SdkTelemetryRegistry.Attributes.SoapResponseBodySize, System.Text.Encoding.UTF8.GetByteCount(respStr));
            if (settings.LogSensitiveData)
            {
                // Password-redacted even here: the flag opens the envelope, it does not open the credential.
                activity?.SetReturnValue(escapeXMl(SecurityHelper.RedactPassword(respStr)));
            }

            var respObj = Serialization.DeserializeXml<ResponseEnvelope<RESP>>(respStr)
                ?? throw new ErrorWithCodeException(Errors.XML_DESERIALIZE_ERROR,
                    $"The {soapAction} response did not deserialize to a SOAP envelope.");
            // An envelope whose Body is empty, or whose body element carries a name the deserializer never
            // matched, leaves Body at its `default!` - null for every generated message type. Unrefused, that
            // null walks into each service's mapping and surfaces as a bare NullReferenceException with no
            // cause in it; one service folded it to a clean `false`. Refused here, every service gets the
            // same answer, and the answer names the call.
            return respObj.Body
                ?? throw new ErrorWithCodeException(Errors.XML_DESERIALIZE_ERROR,
                    $"The {soapAction} response carried no <Body> the deserializer could match.");
        }
    }
}