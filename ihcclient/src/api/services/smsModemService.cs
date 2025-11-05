using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Smsmodem;
using System.Diagnostics;

namespace Ihc
{
    /// <summary>
    /// High-level interface for IHC SMS modem service operations.
    /// Used for control of SMS modem
    /// </summary>
    public interface ISmsModemService : IIHCApiService
    {
        /// <summary>
        /// Set SMS modem settings.
        /// </summary>
        /// <param name="settings">SMS modem settings to configure</param>
        Task SetSmsModemSettings(SmsModemSettings settings);

        /// <summary>
        /// Get SMS modem settings.
        /// </summary>
        Task<SmsModemSettings> GetSmsModemSettings();

        /// <summary>
        /// Get SMS modem status information.
        /// </summary>
        Task<SmsModemStatus> GetSmsModemStatus();

        /// <summary>
        /// Get SMS modem hardware and firmware information.
        /// </summary>
        Task<SmsModemInfo> GetSmsModemInfo();

        /// <summary>
        /// Reset the SMS modem.
        /// </summary>
        Task ResetSmsModem();
    }

    public class SmsModemService : ServiceBase, ISmsModemService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Smsmodem.SMSModemService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "SMSModemService") { }

            public Task<outputMessageName1> setSMSModemSettingsAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("setSMSModemSettings", request);
            }

            public Task<outputMessageName2> getSMSModemSettingsAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getSMSModemSettings", request);
            }

            public Task<outputMessageName3> getSMSModemStatusAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getSMSModemStatus", request);
            }

            public Task<outputMessageName4> getSMSModemInfoAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("getSMSModemInfo", request);
            }

            public Task<outputMessageName5> resetSMSModemAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("resetSMSModem", request);
            }
        }

        private readonly SoapImpl impl;

        /// <summary>
        /// Create a SmsModemService instance for access to the IHC API related to SMS modem operations.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public SmsModemService(IAuthenticationService authService)
            : base(authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        // Map methods for translating between SOAP models and high-level models

        private SmsModemSettings MapSettings(WSSMSModemSettings ws)
        {
            if (ws == null)
                return null;

            return new SmsModemSettings
            {
                PowerupMessage = ws.m_powerupMessage,
                PowerdownMessage = ws.m_powerdownMessage,
                PowerdownNumber = ws.m_powerdownNumber,
                RelaySMS = ws.m_relaySMS,
                ForceStandAloneMode = ws.m_forceStandAloneMode,
                SendLowBatteryNotification = ws.m_sendLowBatteryNotification,
                SendLowBatteryNotificationLanguage = ws.m_sendLowBatteryNotificationLanguage,
                SendLEDDimmerErrorNotification = ws.m_sendLEDDimmerErrorNotification
            };
        }

        private WSSMSModemSettings MapSettings(SmsModemSettings settings)
        {
            return new WSSMSModemSettings
            {
                m_powerupMessage = settings.PowerupMessage,
                m_powerdownMessage = settings.PowerdownMessage,
                m_powerdownNumber = settings.PowerdownNumber,
                m_relaySMS = settings.RelaySMS,
                m_forceStandAloneMode = settings.ForceStandAloneMode,
                m_sendLowBatteryNotification = settings.SendLowBatteryNotification,
                m_sendLowBatteryNotificationLanguage = settings.SendLowBatteryNotificationLanguage,
                m_sendLEDDimmerErrorNotification = settings.SendLEDDimmerErrorNotification
            };
        }

        private SmsModemStatus MapStatus(WSSMSModemStatus ws)
        {
            if (ws == null)
                return null;

            return new SmsModemStatus
            {
                AntennaCoverage = ws.antennaCoverage,
                MobileOperator = ws.mobileOperator,
                ModemStatus = ws.modemStatus,
                MobileNumber = ws.mobileNumber
            };
        }

        private SmsModemInfo MapInfo(WSSMSModemInfo ws)
        {
            if (ws == null)
                return null;

            return new SmsModemInfo
            {
                FirmwareVersion = ws.firmwareVersion,
                GSMChipVersion = ws.gSMChipVersion,
                HardwareRevision = ws.hardwareRevision,
                ProductionDate = ws.productionDate,
                Detected = ws.detected,
                SerialNumber = ws.serialNumber,
                IMEINumber = ws.iMEINumber
            };
        }

        public async Task SetSmsModemSettings(SmsModemSettings settings)
        {
            using (var activity = StartActivity(nameof(SetSmsModemSettings)))
            {
                try
                {
                    activity?.SetParameters((nameof(settings), settings));

                    var wsSettings = MapSettings(settings);
                    await impl.setSMSModemSettingsAsync(new inputMessageName1 { setSMSModemSettings1 = wsSettings }).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<SmsModemSettings> GetSmsModemSettings()
        {
            using (var activity = StartActivity(nameof(GetSmsModemSettings)))
            {
                try
                {
                    var resp = await impl.getSMSModemSettingsAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = MapSettings(resp.getSMSModemSettings1);

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

        public async Task<SmsModemStatus> GetSmsModemStatus()
        {
            using (var activity = StartActivity(nameof(GetSmsModemStatus)))
            {
                try
                {
                    var resp = await impl.getSMSModemStatusAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = MapStatus(resp.getSMSModemStatus1);

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

        public async Task<SmsModemInfo> GetSmsModemInfo()
        {
            using (var activity = StartActivity(nameof(GetSmsModemInfo)))
            {
                try
                {
                    var resp = await impl.getSMSModemInfoAsync(new inputMessageName4()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = MapInfo(resp.getSMSModemInfo1);

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

        public async Task ResetSmsModem()
        {
            using (var activity = StartActivity(nameof(ResetSmsModem)))
            {
                try
                {
                    await impl.resetSMSModemAsync(new inputMessageName5()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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