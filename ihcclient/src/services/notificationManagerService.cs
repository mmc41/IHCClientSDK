using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Notificationmanager;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC NotificationManagerService without any of the soap distractions.
    */
    public interface INotificationManagerService
    {
        public Task ClearMessages();
    
        public Task<NotificationMessage[]> GetMessages();
    }

    /**
    * A highlevel implementation of a client to the IHC NotificationManagerService without exposing any of the soap distractions.
    *
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

        private NotificationMessage mapMessage(WSNotificationMessage e)
        {
            return new NotificationMessage()
            {
                Date = mapDate(e.date),
                NotificationType = e.notificationType,
                Recipient = e.recipient,
                Sender = e.sender,
                Subject = e.subject,
                Body = e.body,
                Delivered = e.delivered
            };
        }

        private DateTimeOffset mapDate(WSDate v)
        {
            return new DateTimeOffset(v.year, v.monthWithJanuaryAsOne, v.day, v.hours, v.minutes, v.seconds, DateHelper.GetWSTimeOffset());
        }

        public async Task ClearMessages()
        {
            await impl.clearMessagesAsync(new inputMessageName2());
        }       

        public async Task<NotificationMessage[]> GetMessages()
        {
            var resp = await impl.getMessagesAsync(new inputMessageName1());
            return resp.getMessages1.Where((v) => v != null).Select((v) => mapMessage(v)).ToArray();
        }
    }
}