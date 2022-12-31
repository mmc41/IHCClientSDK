using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Module;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ModuleService without any of the soap distractions.
    *
    * TODO: Add operations.
    */
    public interface IModuleService
    {

    }

    /**
    * A highlevel implementation of a client to the IHC ModuleService without exposing any of the soap distractions.
    *
    * TODO: Add operations (see other services for inspiration)
    */
    public class ModuleService : IModuleService {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Module.ModuleService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "ModuleService") {}

            public Task<outputMessageName6> clearAllAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("clearAll", request);
            }

            public Task<outputMessageName5> getSceneProjectAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getSceneProject", request);
            }

            public Task<outputMessageName1> getSceneProjectInfoAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getSceneProjectInfo", request);
            }

            public Task<outputMessageName3> getSceneProjectSegmentAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getSceneProjectSegment", request);
            }

            public Task<outputMessageName7> getSceneProjectSegmentationSizeAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("getSceneProjectSegmentationSize", request);
            }

            public Task<outputMessageName2> storeSceneProjectAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("storeSceneProject", request);
            }

            public Task<outputMessageName4> storeSceneProjectSegmentAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("storeSceneProjectSegment", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an ModuleService instance for access to the IHC API related to projects.
        * <param name="authService">AuthenticationService instance</param>
        */
        public ModuleService(IAuthenticationService authService) {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Implement high level services.

    }
}