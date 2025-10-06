using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.AirlinkManagement;

namespace Ihc
{
    public interface IAirlinkManagementService
    {
        // TODO: Insert high level operations for Airlink Management
    }

    public class AirlinkManagementService : IAirlinkManagementService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl //, Ihc.Soap.AirlinkManagement.AirlinkManagementService
        {
            // TODO: Ihc.Soap.AirlinkManagement.AirlinkManagementService
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "AirlinkManagementService") { }

        }

        private readonly SoapImpl impl;

        /**
        * Create an ConfigurationService instance for access to the IHC API related to configuration.
        * <param name="authService">AuthenticationService instance</param>
        */
        public AirlinkManagementService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Insert high level operations for Airlink Management. These should call impl.<operation>Async(...) and transalte Soap models to new highlevel models or basic types
    }
}