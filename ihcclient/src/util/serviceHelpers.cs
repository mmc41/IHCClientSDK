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
       public IReadOnlyList<SeviceOperationMetadata> GetOperations();
    }

    public abstract class ServiceBase : IIHCService
    {
        protected ServiceBase() { }

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
        protected readonly bool asyncContinueOnCapturedContext;

        protected ServiceBaseImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, string serviceName, bool asyncContinueOnCapturedContext)
        {
            this.logger = logger;
            this.Url = endpoint + "/ws/" + serviceName;
            this.cookieHandler = cookieHandler;
            this.asyncContinueOnCapturedContext = asyncContinueOnCapturedContext;
            this.ihcClient = new Client(logger, cookieHandler, Url, asyncContinueOnCapturedContext);
        }

        /**
         * Soap HTTP post action.
         */
        protected async Task<RESP> soapPost<RESP, REQ>(string soapAction, REQ request, OnOkCallBack onOkSideEffect = null)
        {
            var req = Serialization.SerializeXml<RequestEnvelope<REQ>>(new RequestEnvelope<REQ>(request));

            var httpResp = await ihcClient.Post(soapAction, req).ConfigureAwait(asyncContinueOnCapturedContext);

            httpResp.EnsureSuccessStatusCode();

            if (onOkSideEffect != null)
            {
                onOkSideEffect(httpResp);
            }

            string respStr = await httpResp.Content.ReadAsStringAsync().ConfigureAwait(asyncContinueOnCapturedContext);

            var respObj = Serialization.DeserializeXml<ResponseEnvelope<RESP>>(respStr);

            return respObj.Body;
        }
    }
}