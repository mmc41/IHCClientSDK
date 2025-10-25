using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Messagecontrollog;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC MessageControlLogService without any of the soap distractions.
    /// </summary>
    public interface IMessageControlLogService : IIHCService
    {
        /// <summary>
        /// Clear all entries from the message control log.
        /// </summary>
        public Task EmptyLog();

        /// <summary>
        /// Get all message control log event entries.
        /// </summary>
        public Task<LogEventEntry[]> GetEvents();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC MessageControllogService without exposing any of the soap distractions.
    /// </summary>
    public class MessageControlLogService : ServiceBase, IMessageControlLogService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Messagecontrollog.MessageControlLogService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "MessageControlLogService") { }

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

        /// <summary>
        /// Create an Messagecontrollog instance for access to the IHC API related to messages.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public MessageControlLogService(IAuthenticationService authService)
            : base(authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
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
            using (var activity = StartActivity(nameof(EmptyLog)))
            {
                try
                {
                    await impl.emptyLogAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<LogEventEntry[]> GetEvents()
        {
            using (var activity = StartActivity(nameof(GetEvents)))
            {
                try
                {
                    var resp = await impl.getEventsAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getEvents1.Where((v) => v != null).Select((v) => mapEvent(v)).ToArray();

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