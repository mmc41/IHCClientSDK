using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ihc 
{
    public interface IIHCService
    {
        /**
        * The IhcSettings used
        */
        public IhcSettings IhcSettings { get; }

        /**
        * The logger used (supplied to constructor in default implementation).
        */
        public ILogger Logger { get; }
    }

    public abstract class ServiceBase : IIHCService
    {
        protected readonly ILogger logger;

        protected readonly IhcSettings settings;

        protected ServiceBase(ILogger logger, IhcSettings settings)
        {
            this.logger = logger;
            this.settings = settings;

            if (this.settings == null)
            {
                throw new ArgumentException("IhcSettings must be supplied");
            }

            if (this.settings.Endpoint == null || this.settings.Application == null)
            {
                throw new ArgumentException("IhcSettings fields Endpoint, Application must be supplied");
            }

            if (logger == null)
            {
                throw new ArgumentException("ILogger must be supplied");
            }
        }
       
        public IhcSettings IhcSettings
        {
            get { return settings; }
        }

        public ILogger Logger { get { return logger; } }

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
        protected readonly ILogger logger;
        protected readonly ICookieHandler cookieHandler;
        public readonly string Url;
        protected IhcSettings settings;


        protected ServiceBaseImpl(ILogger logger, ICookieHandler cookieHandler, IhcSettings settings, string serviceName)
        {
            this.logger = logger;
            this.settings = settings;
            this.Url = settings.Endpoint + "/ws/" + serviceName;
            this.cookieHandler = cookieHandler;
            this.ihcClient = new Client(logger, cookieHandler, Url, settings);
        }

        /**
         * Soap HTTP post action.
         */
        protected async Task<RESP> soapPost<RESP, REQ>(string soapAction, REQ request, OnOkCallBack onOkSideEffect = null)
        {
            var req = Serialization.SerializeXml<RequestEnvelope<REQ>>(new RequestEnvelope<REQ>(request));

            var httpResp = await ihcClient.Post(soapAction, req).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            httpResp.EnsureSuccessStatusCode();

            if (onOkSideEffect != null)
            {
                onOkSideEffect(httpResp);
            }

            string respStr = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            var respObj = Serialization.DeserializeXml<ResponseEnvelope<RESP>>(respStr);

            return respObj.Body;
        }
    }
}