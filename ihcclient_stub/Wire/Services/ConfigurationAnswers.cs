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

        /// <summary>The log as the controller sends it: its lines in one UTF-8 file, which the SDK splits again.</summary>
        internal Task<outputMessageName2> getUserLogAsync(inputMessageName2 request) =>
            Task.FromResult(new outputMessageName2
            {
                getUserLog4 = new WSFile { filename = "userlog.0", data = Encoding.UTF8.GetBytes(string.Join('\n', scenario.UserLog)) },
            });

        internal Task<outputMessageName3> clearUserLogAsync(inputMessageName3 request)
        {
            scenario.UserLog = [];
            return Task.FromResult(new outputMessageName3());
        }

        internal Task<outputMessageName12> getWLanInterfaceAsync(inputMessageName12 request) =>
            Task.FromResult(new outputMessageName12 { getWLanInterface1 = WLanInterfaceToWire(scenario.WLanInterface) });

        internal Task<outputMessageName13> getWLanScanAsync(inputMessageName13 request) =>
            Task.FromResult(new outputMessageName13 { getWLanScan1 = [.. scenario.WLanScan.Select(WLanCellToWire)] });

        internal Task<outputMessageName11> testSendMessage1Async(inputMessageName11 request)
        {
            AnswerContext.RecordPayload(new NotificationMessage
            {
                Recipient = request.testSendMessage8?.recipient,
                Subject = request.testSendMessage9,
                Body = request.testSendMessage10,
            });
            bool sent = scenario.TestSendSucceeds;
            return Task.FromResult(new outputMessageName11
            {
                testSendMessage12 = new WSNotificationMessageSendResult { attemptSucceeded = sent, attemptFailedDueToNetworkProblems = !sent },
            });
        }

        /// <summary>The captured status of a mail server out of reach; the SDK reads nothing from it.</summary>
        internal static Task<outputMessageName9> testSettingsNowAsync(inputMessageName9 request) =>
            Task.FromResult(new outputMessageName9
            {
                testSettingsNow2 = new WSEmailControlConnectionStatus { attemptFailedDueToNetworkProblems = true },
            });

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
