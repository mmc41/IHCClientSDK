using System.Threading.Tasks;
using System;
using Ihc.Soap.Messagecontrollog;
using System.Diagnostics;
using System.Collections.Generic;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC MessageControlLogService without any of the soap distractions.
    /// </summary>
    public interface IMessageControlLogService : IIHCApiService
    {
        /// <summary>
        /// Clear all entries from the message control log.
        /// </summary>
        public Task EmptyLog();

        /// <summary>
        /// Get all message control log event entries.
        /// </summary>
        public Task<IReadOnlyList<LogEventEntry>> GetEvents();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC MessageControllogService without exposing any of the soap distractions.
    /// </summary>
    public class MessageControlLogService : ServiceBase, IMessageControlLogService
    {
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

        private readonly Ihc.Soap.Messagecontrollog.MessageControlLogService impl;

        /// <summary>
        /// Create an Messagecontrollog instance for access to the IHC API related to messages.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public MessageControlLogService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal MessageControlLogService(IAuthenticationService authService, Ihc.Soap.Messagecontrollog.MessageControlLogService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        private static LogEventEntry? mapEvent(WSMessageControlLogEntry? e)
        {
            if (e == null)
                return null;

            return new LogEventEntry()
            {
                Date = DateHelper.OrAbsentSentinel(e.date?.ToDateTimeOffset(), nameof(LogEventEntry.Date)),
                ControlType = e.controlType,
                LogEntryType = e.logEntryType,
                SenderAddress = e.senderAddress?.address,
                SenderAddressDescription = e.senderAddress?.description,
                TriggerString = e.triggerString,
                AuthenticationTypeAsString = e.authenticationTypeAsString,
                ActionTypeAsString = e.actionTypeAsString
            };
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

        public async Task<IReadOnlyList<LogEventEntry>> GetEvents()
        {
            using (var activity = StartActivity(nameof(GetEvents)))
            {
                try
                {
                    var resp = await impl.getEventsAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Through the shared mapper, so the DROPS are counted. A null entry in the wire list is
                    // discarded - there is nothing to map - and the discarded entries used to leave the caller a
                    // shorter log with nothing saying so, which for a log is the one loss that matters.
                    var retv = WireList.MapPresent(resp.getEvents1, mapEvent, activity, nameof(GetEvents));

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