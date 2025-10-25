using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace Ihc 
{
    public interface IIHCService
    {
        /**
        * The IhcSettings used
        */
        public IhcSettings IhcSettings { get; }
    }

    public abstract class ServiceBase : IIHCService
    {
        protected readonly IhcSettings settings;

        protected ServiceBase(IhcSettings settings)
        {
            this.settings = settings;

            if (this.settings == null)
            {
                throw new ArgumentException("IhcSettings must be supplied");
            }

            if (this.settings.Endpoint == null || this.settings.Application == null)
            {
                throw new ArgumentException("IhcSettings fields Endpoint, Application must be supplied");
            }

            if (this.settings.Endpoint.StartsWith(SpecialEndpoints.MockedPrefix))
            {
                throw new ArgumentException("IhcSettings specifies a mocked implmentation which does not correspond to this real implemenentation");
            }
        }

        public IhcSettings IhcSettings
        {
            get { return settings; }
        }

        protected Activity StartActivity(string operationName)
        {
            Activity activity = Telemetry.ActivitySource.StartActivity(this.GetType().Name+"."+operationName, ActivityKind.Internal);
            activity?.SetTag("service.name", this.GetType().Name); // Set name of IHC webservice highlevel wrapper as telemetry service name.
            activity?.SetTag("service.operation", operationName); // Set name of IHC webservice highlevel wrapper operation.          
            return activity;
        }
    }

    /**
    * Callback interface for doing sideeffects as part of a soap http post. 
    */
    public delegate void OnOkCallBack(HttpResponseMessage msg);

    /**
    * Common baseclass for service implementations of IHC soap interfaces.
    */
    internal abstract class ServiceBaseImpl
    {
        protected readonly Client ihcClient;
        protected readonly ICookieHandler cookieHandler;
        public readonly string Url;
        protected IhcSettings settings;


        protected ServiceBaseImpl(ICookieHandler cookieHandler, IhcSettings settings, string serviceName)
        {
            this.settings = settings;
            this.Url = settings.Endpoint + "/ws/" + serviceName;
            this.cookieHandler = cookieHandler;
            this.ihcClient = new Client(cookieHandler, Url, settings);
        }

        private string escapeXMl(string xmlString)
        {
            return System.Security.SecurityElement.Escape(xmlString);
        }

        /**
         * Soap HTTP post action.
         */
        protected async Task<RESP> soapPost<RESP, REQ>(string soapAction, REQ request, OnOkCallBack onOkSideEffect = null)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(nameof(soapPost)+"."+soapAction, ActivityKind.Internal);

            try
            {
                var req = Serialization.SerializeXml<RequestEnvelope<REQ>>(new RequestEnvelope<REQ>(request));

                activity.SetParameters(
                    (nameof(soapAction), soapAction),
                    (nameof(request), escapeXMl(settings.LogSensitiveData ? req : SecurityHelper.RedactPassword(req))), // Use escaped string representation of request for activity logging.
                    (nameof(onOkSideEffect), onOkSideEffect != null)
                );

                var httpResp = await ihcClient.Post(soapAction, req).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                httpResp.EnsureSuccessStatusCode();

                if (onOkSideEffect != null)
                {
                    onOkSideEffect(httpResp);
                }

                string respStr = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                activity?.SetReturnValue(escapeXMl(SecurityHelper.RedactPassword(respStr))); // Use escaped string representation of response for activity logging.

                var respObj = Serialization.DeserializeXml<ResponseEnvelope<RESP>>(respStr);
                return respObj.Body;
            } catch (Exception ex)
            {
                activity.SetError(ex);
                throw;
            }
        }
    }
}