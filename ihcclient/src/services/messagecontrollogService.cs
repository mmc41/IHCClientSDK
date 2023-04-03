using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Messagecontrollog;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC MessageControlLogService without any of the soap distractions.
    *
    * Status: 100% API coverage but not fully tested or documented.
    */
    public interface IMessageControlLogService
    {
        public Task EmptyLog();
        public Task<LogEventEntry[]> GetEvents();
    }

    /**
    * A highlevel implementation of a client to the IHC MessageControllogService without exposing any of the soap distractions.
    */
    public class MessageControlLogService : IMessageControlLogService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Messagecontrollog.MessageControlLogService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "MessageControlLogService") { }

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
        */
        public MessageControlLogService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        private LogEventEntry mapEvent(WSMessageControlLogEntry e)
        {
            return new LogEventEntry()
            {
                Date = mapDate(e.date),
                ControlType = e.controlType,
                LogEntryType = e.logEntryType,
                SenderAddress = e.senderAddress.address,
                SenderAddressDescription = e.senderAddress.description,
                TriggerString = e.triggerString,
                AuthenticationTypeAsString = e.authenticationTypeAsString,
                ActionTypeAsString = e.actionTypeAsString
            };
        }

        private DateTimeOffset mapDate(WSDate v)
        {
            return new DateTimeOffset(v.year, v.monthWithJanuaryAsOne, v.day, v.hours, v.minutes, v.seconds, DateHelper.GetWSTimeOffset());
        }

        public async Task EmptyLog()
        {
            await impl.emptyLogAsync(new inputMessageName1());
        }

        public async Task<LogEventEntry[]> GetEvents()
        {
            var resp = await impl.getEventsAsync(new inputMessageName2());
            return resp.getEvents1.Where((v) => v != null).Select((v) => mapEvent(v)).ToArray();
        }
    }
}