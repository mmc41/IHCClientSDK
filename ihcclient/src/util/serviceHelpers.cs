using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Ihc 
{
    public interface IIHCService
    {
       /// <summary>
       /// Get metadata about the operations supported by this service.
       /// For use by test and documentation tools. Not for normal application code.
       /// </summary>
       /// <returns></returns>
       public IReadOnlyList<SeviceOperationMetadata> GetOperations();
    }

    public abstract class ServiceBase : IIHCService
    {
        protected readonly ILogger logger;

        protected IhcSettings settings;

        protected ServiceBase(ILogger logger, IhcSettings settings)
        {
            this.logger = logger;
            this.settings = settings;
        }

        public IReadOnlyList<SeviceOperationMetadata> GetOperations()
        {
            return ServiceMetadata.GetOperations(this);
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