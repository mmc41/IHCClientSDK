using System.Text;
using Ihc.Soap.Configuration;

namespace Ihc.Stubbing
{
    /// <summary>The configuration service's single-call answers, read from and written to <see cref="StubConfiguration"/>.</summary>
    internal sealed partial class ConfigurationAnswers(StubConfiguration scenario) : IWireAnswers
    {
        public string Service => "ConfigurationService";

        internal Task<outputMessageName6> getSystemInfoAsync(inputMessageName6 request) =>
            Task.FromResult(new outputMessageName6 { getSystemInfo1 = SystemInfoToWire(scenario.SystemInfo) });

        /// <summary>
        /// The log as the controller sends it: its lines in one UTF-8 file, which the SDK splits again, named by the
        /// arranged next cursor. The cursor sent is journalled; it selects nothing, so every read answers every line.
        /// </summary>
        internal Task<outputMessageName2> getUserLogAsync(inputMessageName2 request)
        {
            AnswerContext.RecordPayload(request.getUserLog1);
            return Task.FromResult(new outputMessageName2
            {
                getUserLog4 = new WSFile { filename = scenario.UserLogCursor, data = Encoding.UTF8.GetBytes(string.Join('\n', scenario.UserLog)) },
            });
        }

        internal Task<outputMessageName3> clearUserLogAsync(inputMessageName3 request)
        {
            scenario.UserLog = [];
            return Task.FromResult(new outputMessageName3());
        }

        internal Task<outputMessageName12> getWLanInterfaceAsync(inputMessageName12 request) =>
            Task.FromResult(new outputMessageName12 { getWLanInterface1 = WLanInterfaceToWire(scenario.WLanInterface) });

        internal Task<outputMessageName13> getWLanScanAsync(inputMessageName13 request) =>
            Task.FromResult(new outputMessageName13 { getWLanScan1 = [.. scenario.WLanScan.Select(WLanCellToWire)] });

        /// <summary>
        /// The six-part test mail IHC Administrator sends: journals the message and the SMTP account it names, the
        /// account's password cleared here because the journal clears only a payload's own string members, and
        /// answers the arranged outcome. A test stores nothing.
        /// </summary>
        internal Task<outputMessageName10> testSendMessageAsync(inputMessageName10 request)
        {
            WSNotificationMessage? message = request.testSendMessage1;
            AnswerContext.RecordPayload(new StubTestMail(
                new NotificationMessage
                {
                    NotificationType = message?.notificationType,
                    Recipient = message?.recipient,
                    Sender = message?.sender,
                    Subject = message?.subject,
                    Body = message?.body,
                    Delivered = message?.delivered ?? false,
                },
                new SMTPSettings
                {
                    Hostname = request.testSendMessage2,
                    Hostport = request.testSendMessage3 ?? 0,
                    Username = request.testSendMessage4,
                    Ssl = request.testSendMessage6 ?? false,
                }));
            bool sent = scenario.TestSendSucceeds;
            return Task.FromResult(new outputMessageName10
            {
                testSendMessage7 = new WSNotificationMessageSendResult { attemptSucceeded = sent, attemptFailedDueToNetworkProblems = !sent },
            });
        }

        /// <summary>Journals the settings to test and answers the arranged status; a test stores nothing.</summary>
        internal Task<outputMessageName9> testSettingsNowAsync(inputMessageName9 request)
        {
            AnswerContext.RecordPayload(EmailControlFromWire(request.testSettingsNow1));
            return Task.FromResult(new outputMessageName9
            {
                testSettingsNow2 = scenario.EmailControlTestStatus is { } status
                    ? new WSEmailControlConnectionStatus
                    {
                        lastConnectionAttemptSucceeded = status.LastConnectionAttemptSucceeded,
                        attemptFailedDueToAuthentication = status.AttemptFailedDueToAuthentication,
                        attemptFailedDueToNetworkProblems = status.AttemptFailedDueToNetworkProblems,
                        attemptFailedDueToInvalidSettings = status.AttemptFailedDueToInvalidSettings,
                    }
                    : null,
            });
        }

        internal static Task<outputMessageName1> delayedRebootAsync(inputMessageName1 request)
        {
            AnswerContext.RecordPayload(request.delayedReboot1);
            return Task.FromResult(new outputMessageName1());
        }

        private static WSSystemInfo SystemInfoToWire(SystemInfo info) => new()
        {
            uptime = info.Uptime,
            realtimeclock = info.Realtimeclock.UtcDateTime,
            serialNumber = info.SerialNumber,
            productionDate = info.ProductionDate,
            brand = info.Brand,
            version = info.Version,
            hwRevision = info.HWRevision,
            swDate = info.SWDate.UtcDateTime,
            datalineVersion = info.DatalineVersion,
            rfModuleSoftwareVersion = info.RFModuleSoftwareVersion,
            rfModuleSerialNumber = info.RFModuleSerialNumber,
            applicationIsWithoutViewer = info.ApplicationIsWithoutViewer,
            smsModemSoftwareVersion = info.SmsModemSoftwareVersion,
            ledDimmerSoftwareVersion = info.LedDimmerSoftwareVersion,
        };

        private static WSWLanInterface? WLanInterfaceToWire(WLanInterface? wlan) => wlan is null ? null : new WSWLanInterface
        {
            connected = wlan.Connected,
            name = wlan.Name,
            ssid = wlan.Ssid,
            quality = wlan.Quality,
        };

        private static WSWLanCell WLanCellToWire(WLanCell cell) => new()
        {
            ssid = cell.Ssid,
            hasEncryption = cell.HasEncryption,
            securityType = cell.SecurityType,
            encryptionType = cell.EncryptionType,
        };
    }
}
