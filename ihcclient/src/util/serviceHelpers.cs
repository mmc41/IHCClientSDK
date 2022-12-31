using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using Microsoft.Extensions.Logging;

namespace Ihc 
{
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

        protected ServiceBaseImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, string serviceName) {
            this.logger = logger;
            this.Url = endpoint + "/ws/" + serviceName;
            this.cookieHandler = cookieHandler;
            this.ihcClient = new Client(logger, cookieHandler, Url);
        }

        /**
         * Soap HTTP post action.
         */
        protected async Task<RESP> soapPost<RESP, REQ>(string soapAction, REQ request, OnOkCallBack onOkSideEffect = null)
        {
            var req = Serialization.SerializeXml<RequestEnvelope<REQ>>(new RequestEnvelope<REQ>(request));

            var httpResp = await ihcClient.Post(soapAction, req);

            httpResp.EnsureSuccessStatusCode();

            if (onOkSideEffect!=null) {
                onOkSideEffect(httpResp);
            }

            string respStr = await httpResp.Content.ReadAsStringAsync();

            var respObj = Serialization.DeserializeXml<ResponseEnvelope<RESP>>(respStr);

            return respObj.Body;
        }
    }
}