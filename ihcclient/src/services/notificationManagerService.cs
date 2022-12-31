using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Notificationmanager;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC NotificationManagerService without any of the soap distractions.
    *
    * TODO: Add operations.
    */
    public interface INotificationManagerService
    {

    }

    /**
    * A highlevel implementation of a client to the IHC NotificationManagerService without exposing any of the soap distractions.
    *
    * TODO: Add operations (see other services for inspiration).
    */
    public class NotificationManagerService : INotificationManagerService {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Notificationmanager.NotificationManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "NotificationManagerService") {}

            public Task<outputMessageName2> clearMessagesAsync(inputMessageName2 request)
            {
              return soapPost<outputMessageName2, inputMessageName2>("clearMessages", request);
            }

            public Task<outputMessageName1> getMessagesAsync(inputMessageName1 request)
            {
              return soapPost<outputMessageName1, inputMessageName1>("getMessages", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an NotificationManagerService instance for access to the IHC API related to notifications.
        * <param name="authService">AuthenticationService instance</param>
        */
        public NotificationManagerService(IAuthenticationService authService) {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Implement high level service.

    }
}