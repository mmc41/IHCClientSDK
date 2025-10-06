using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Openapi;
namespace Ihc {
    /**
    * A highlevel client interface for the IHC OpenAPIService for v3.0+ controllers without any of the soap distractions.
    *
    * Does not appear to be fully functional or complete. Probably best to use AuthenticationService instead.
    *
    * Nb. Supported by v3.0+ controllers only.
    *
    */
    public interface IOpenAPIService : ICookieHandlerService
    {
        public Task Authenticate(string userName, string password);
        public Task<FWVersion> GetFWVersion();
        public Task<string> GetAPIVersion();
        public Task<TimeSpan> GetUptime();
        public Task<DateTime> GetTime();
        public Task<bool> IsIHCProjectAvailable();
        public Task<int[]> GetDatalineInputIDs();
        public Task<int[]> GetDatalineOutputIDs();
        public Task DoReboot();
        public Task Ping();
        public string Endpoint { get; }
    }

    /**
    * A highlevel implementation of a client to the IHC OpenAPIService without exposing any of the soap distractions.
    *
    * Nb. Supported by v3.0+ controllers only.
    *
    * Core operations are implemented. Advanced project segment operations are available via the underlying SoapImpl if needed.
    */
    public class OpenAPIService : IOpenAPIService
    {
        private readonly ILogger logger;
        private readonly string endpoint;
        private readonly ICookieHandler cookieHandler;

        public ICookieHandler GetCookieHandler()
        {
            return cookieHandler;
        }

        public string Endpoint { 
          get {
            return endpoint;
          } 
        }

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Openapi.OpenAPIService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "OpenAPIService") { }

            public Task<outputMessageName13> authenticateAsync(inputMessageName13 request)
            {
                string cookie = null;

                var result = soapPost<outputMessageName13, inputMessageName13>("authenticate", request, resp =>
                {
                    // Use side-effect to capture cookie sice our post call only captures xml response.
                    cookie = resp.Headers.GetValues("Set-Cookie").FirstOrDefault();
                });

                return result.ContinueWith<outputMessageName13>((r) =>
                {
                    var result = r.Result;
                    // Add cookie only on success.
                    if (result.authenticate3.HasValue && result.authenticate3.Value)
                    {
                        cookieHandler.SetCookie(cookie);
                    }
                    return result;
                });
            }

            public Task<outputMessageName2> disableSubscriptionAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("disableSubscription", request);
            }

            public Task<outputMessageName15> doRebootAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("doReboot", request);
            }

            public Task<outputMessageName1> enableSubscriptionAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("enableSubscription", request);
            }

            public Task<outputMessageName12> getAPIVersionAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("getAPIVersion", request);
            }

            public Task<outputMessageName3> getDatalineInputIDsAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getDatalineInputIDs", request);
            }

            public Task<outputMessageName4> getDatalineOutputIDsAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("getDatalineOutputIDs", request);
            }

            public Task<outputMessageName11> getFWVersionAsync(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("getFWVersion", request);
            }

            public Task<outputMessageName19> getIHCProjectNumberOfSegmentsAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("getIHCProjectNumberOfSegments", request);
            }

            public Task<outputMessageName17> getIHCProjectSegmentAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("getIHCProjectSegment", request);
            }

            public Task<outputMessageName18> getIHCProjectSegmentationSizeAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("getIHCProjectSegmentationSize", request);
            }

            public Task<outputMessageName14> getProjectInfoAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("getProjectInfo", request);
            }

            public Task<outputMessageName20> getSceneProjectInfoAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("getSceneProjectInfo", request);
            }

            public Task<outputMessageName22> getSceneProjectSegmentAsync(inputMessageName22 request)
            {
                return soapPost<outputMessageName22, inputMessageName22>("getSceneProjectSegment", request);
            }

            public Task<outputMessageName21> getSceneProjectSegmentationSizeAsync(inputMessageName21 request)
            {
                return soapPost<outputMessageName21, inputMessageName21>("getSceneProjectSegmentationSize", request);
            }

            public Task<outputMessageName8> getTimeAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("getTime", request);
            }

            public Task<outputMessageName9> getUptimeAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("getUptime", request);
            }

            public Task<outputMessageName6> getValuesAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("getValues", request);
            }

            public Task<outputMessageName16> isIHCProjectAvailableAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("isIHCProjectAvailable", request);
            }

            public Task<outputMessageName10> pingAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("ping", request);
            }

            public Task<outputMessageName7> setValuesAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("setValues", request);
            }

            public Task<outputMessageName5> waitForEventsAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("waitForEvents", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an OpenAPIService instance for access to the IHC API related to the open api.
        *
        * <param name="logger">A logger instance. Alternatively, use NullLogger<YourClass>.Instance</param>
        * <param name="endpoint">IHC controller endpoint of form http://\<YOUR CONTROLLER IP ADDRESS\></param>
        */
        public OpenAPIService(ILogger logger, string endpoint)
        {
            this.logger = logger;
            this.endpoint = endpoint;
            this.cookieHandler = new CookieHandler(logger);
            this.impl = new SoapImpl(logger, cookieHandler, endpoint);
        }

        public async Task Authenticate(string userName, string password)
        {
            logger.LogInformation("IHC OpenAPI Authenticate called");
            var resp = await impl.authenticateAsync(new inputMessageName13() { authenticate1 = userName, authenticate2 = password });

            if (resp.authenticate3.HasValue && resp.authenticate3.Value)
            {
                logger.LogInformation("IHC OpenAPI authentication successful");
                return;
            }
            else
            {
                logger.LogError("IHC OpenAPI authentication failed for endpoint {Url}", impl.Url);
                throw new ErrorWithCodeException(Errors.LOGIN_UNKNOWN_ERROR, "Ihc server login failed for " + impl.Url);
            }
        }

        public async Task Ping()
        {
            await impl.pingAsync(new inputMessageName10());
            return;
        }

        public async Task<FWVersion> GetFWVersion()
        {
            var result = await impl.getFWVersionAsync(new inputMessageName11());
            return new FWVersion() { MajorVersion = result.getFWVersion1.majorVersion, MinorVersion = result.getFWVersion1.minorVersion, BuildVersion = result.getFWVersion1.buildVersion };
        }

        public async Task<string> GetAPIVersion()
        {
            var result = await impl.getAPIVersionAsync(new inputMessageName12());
            return result.getAPIVersion1.HasValue ? result.getAPIVersion1.Value.ToString() : "0";
        }

        public async Task<TimeSpan> GetUptime()
        {
            var result = await impl.getUptimeAsync(new inputMessageName9());
            return TimeSpan.FromMilliseconds(result.getUptime1.HasValue ? result.getUptime1.Value : 0);
        }

        public async Task<DateTime> GetTime()
        {
            var result = await impl.getTimeAsync(new inputMessageName8());
            return result.getTime1.ToDateTimeOffset().DateTime;
        }

        public async Task<bool> IsIHCProjectAvailable()
        {
            var result = await impl.isIHCProjectAvailableAsync(new inputMessageName16());
            return result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;
        }

        public async Task<int[]> GetDatalineInputIDs()
        {
            var result = await impl.getDatalineInputIDsAsync(new inputMessageName3());
            return result.getDatalineInputIDs1?.Select(r => r.resourceID).ToArray() ?? Array.Empty<int>();
        }

        public async Task<int[]> GetDatalineOutputIDs()
        {
            var result = await impl.getDatalineOutputIDsAsync(new inputMessageName4());
            return result.getDatalineOutputIDs1?.Select(r => r.resourceID).ToArray() ?? Array.Empty<int>();
        }

        public async Task DoReboot()
        {
            logger.LogWarning("IHC OpenAPI DoReboot called - controller will reboot");
            await impl.doRebootAsync(new inputMessageName15());
        }
    }
}