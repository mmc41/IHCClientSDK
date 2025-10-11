using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Notificationmanager;
using System.Diagnostics;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC NotificationManagerService without any of the soap distractions.
    *
    */
    public interface INotificationManagerService : IIHCService
    {
        /**
        * Clear all notification messages from the controller.
        */
        public Task ClearMessages();

        /**
        * Get all notification messages from the controller.
        */
        public Task<NotificationMessage[]> GetMessages();
    }

    /**
    * A highlevel implementation of a client to the IHC NotificationManagerService without exposing any of the soap distractions.
    *
    */
    public class NotificationManagerService : ServiceBase, INotificationManagerService {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Notificationmanager.NotificationManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool logSensitiveData, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "NotificationManagerService", logSensitiveData, asyncContinueOnCapturedContext) {}

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
        * <param name="logSensitiveData">If true, log sensitive data. If false (default), redact sensitive values in logs.</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public NotificationManagerService(IAuthenticationService authService, bool logSensitiveData = false, bool asyncContinueOnCapturedContext = false)
            : base(authService.Logger, logSensitiveData, asyncContinueOnCapturedContext)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, logSensitiveData, asyncContinueOnCapturedContext);
        }

        private NotificationMessage mapMessage(WSNotificationMessage e)
        {
            if (e == null)
                return null;

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
            if (v == null)
                return DateTimeOffset.MinValue;

            return new DateTimeOffset(v.year, v.monthWithJanuaryAsOne, v.day, v.hours, v.minutes, v.seconds, DateHelper.GetWSTimeOffset());
        }

        public async Task ClearMessages()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            await impl.clearMessagesAsync(new inputMessageName2()).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<NotificationMessage[]> GetMessages()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getMessagesAsync(new inputMessageName1()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getMessages1.Where((v) => v != null).Select((v) => mapMessage(v)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }
    }
}