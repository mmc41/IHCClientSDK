using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Messagecontrollog;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC MessageControlLogService without any of the soap distractions.
    */
    public interface IMessageControlLogService
    {
        /**
        * Clear all entries from the message control log.
        */
        public Task EmptyLog();

        /**
        * Get all message control log event entries.
        */
        public Task<LogEventEntry[]> GetEvents();
    }

    /**
    * A highlevel implementation of a client to the IHC MessageControllogService without exposing any of the soap distractions.
    */
    public class MessageControlLogService : IMessageControlLogService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;
        private readonly bool asyncContinueOnCapturedContext;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Messagecontrollog.MessageControlLogService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "MessageControlLogService", asyncContinueOnCapturedContext) { }

            public Task<outputMessageName1> emptyLogAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("emptyLog", request);
            }

            public Task<outputMessageName2> getEventsAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getEvents", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an Messagecontrollog instance for access to the IHC API related to messages.
        * <param name="authService">AuthenticationService instance</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public MessageControlLogService(IAuthenticationService authService, bool asyncContinueOnCapturedContext = false)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.asyncContinueOnCapturedContext = asyncContinueOnCapturedContext;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, asyncContinueOnCapturedContext);
        }

        private LogEventEntry mapEvent(WSMessageControlLogEntry e)
        {
            if (e == null)
                return null;

            return new LogEventEntry()
            {
                Date = mapDate(e.date),
                ControlType = e.controlType,
                LogEntryType = e.logEntryType,
                SenderAddress = e.senderAddress?.address,
                SenderAddressDescription = e.senderAddress?.description,
                TriggerString = e.triggerString,
                AuthenticationTypeAsString = e.authenticationTypeAsString,
                ActionTypeAsString = e.actionTypeAsString
            };
        }

        private DateTimeOffset mapDate(WSDate v)
        {
            if (v == null)
                return DateTimeOffset.MinValue;

            return new DateTimeOffset(v.year, v.monthWithJanuaryAsOne, v.day, v.hours, v.minutes, v.seconds, DateHelper.GetWSTimeOffset());
        }

        public async Task EmptyLog()
        {
            await impl.emptyLogAsync(new inputMessageName1()).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<LogEventEntry[]> GetEvents()
        {
            var resp = await impl.getEventsAsync(new inputMessageName2()).ConfigureAwait(asyncContinueOnCapturedContext);
            return resp.getEvents1.Where((v) => v != null).Select((v) => mapEvent(v)).ToArray();
        }
    }
}