using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Notificationmanager;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC NotificationManagerService without any of the soap distractions.
    /// </summary>
    public interface INotificationManagerService : IIHCService
    {
        /// <summary>
        /// Clear all notification messages from the controller.
        /// </summary>
        public Task ClearMessages();

        /// <summary>
        /// Get all notification messages from the controller.
        /// </summary>
        public Task<NotificationMessage[]> GetMessages();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC NotificationManagerService without exposing any of the soap distractions.
    /// </summary>
    public class NotificationManagerService : ServiceBase, INotificationManagerService {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Notificationmanager.NotificationManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, IhcSettings settings) : base(logger, cookieHandler, settings, "NotificationManagerService") {}

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

        /// <summary>
        /// Create a NotificationManagerService instance for access to the IHC API related to notifications.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public NotificationManagerService(IAuthenticationService authService)
            : base(authService.Logger, authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), settings);
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
            using (var activity = StartActivity(nameof(ClearMessages)))
            {
                try
                {
                    await impl.clearMessagesAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<NotificationMessage[]> GetMessages()
        {
            using (var activity = StartActivity(nameof(GetMessages)))
            {
                try
                {
                    var resp = await impl.getMessagesAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getMessages1.Where((v) => v != null).Select((v) => mapMessage(v)).ToArray();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }
    }
}