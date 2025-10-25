namespace Ihc
{
    /// <summary>
    /// High level model of SMS modem settings without soap distractions.
    /// </summary>
    public record SmsModemSettings
    {
        /// <summary>
        /// Message to send when the system powers up.
        /// </summary>
        public string PowerupMessage { get; init; }

        /// <summary>
        /// Message to send when the system powers down.
        /// </summary>
        public string PowerdownMessage { get; init; }

        /// <summary>
        /// Phone number to send powerdown message to.
        /// </summary>
        public string PowerdownNumber { get; init; }

        /// <summary>
        /// Indicates whether SMS messages should be relayed.
        /// </summary>
        public bool RelaySMS { get; init; }

        /// <summary>
        /// Forces the modem to operate in standalone mode.
        /// </summary>
        public bool ForceStandAloneMode { get; init; }

        /// <summary>
        /// Indicates whether low battery notifications should be sent.
        /// </summary>
        public bool SendLowBatteryNotification { get; init; }

        /// <summary>
        /// Language setting for low battery notifications.
        /// </summary>
        public bool SendLowBatteryNotificationLanguage { get; init; }

        /// <summary>
        /// Indicates whether LED dimmer error notifications should be sent.
        /// </summary>
        public bool SendLEDDimmerErrorNotification { get; init; }

        public override string ToString()
        {
            return $"SmsModemSettings(PowerupMessage={PowerupMessage}, PowerdownMessage={PowerdownMessage}, PowerdownNumber={PowerdownNumber}, RelaySMS={RelaySMS}, ForceStandAloneMode={ForceStandAloneMode}, SendLowBatteryNotification={SendLowBatteryNotification}, SendLowBatteryNotificationLanguage={SendLowBatteryNotificationLanguage}, SendLEDDimmerErrorNotification={SendLEDDimmerErrorNotification})";
        }
    }

    /// <summary>
    /// High level model of SMS modem information without soap distractions.
    /// </summary>
    public record SmsModemInfo
    {
        /// <summary>
        /// Firmware version of the SMS modem.
        /// </summary>
        public string FirmwareVersion { get; init; }

        /// <summary>
        /// Version of the GSM chip in the modem.
        /// </summary>
        public string GSMChipVersion { get; init; }

        /// <summary>
        /// Hardware revision of the modem.
        /// </summary>
        public string HardwareRevision { get; init; }

        /// <summary>
        /// Production date of the modem.
        /// </summary>
        public string ProductionDate { get; init; }

        /// <summary>
        /// Indicates whether the modem has been detected by the system.
        /// </summary>
        public bool Detected { get; init; }

        /// <summary>
        /// Serial number of the modem.
        /// </summary>
        public string SerialNumber { get; init; }

        /// <summary>
        /// IMEI number of the modem (International Mobile Equipment Identity).
        /// </summary>
        public string IMEINumber { get; init; }

        public override string ToString()
        {
            return $"SmsModemInfo(FirmwareVersion={FirmwareVersion}, GSMChipVersion={GSMChipVersion}, HardwareRevision={HardwareRevision}, ProductionDate={ProductionDate}, Detected={Detected}, SerialNumber={SerialNumber}, IMEINumber={IMEINumber})";
        }
    }

    /// <summary>
    /// High level model of SMS modem status without soap distractions.
    /// </summary>
    public record SmsModemStatus
    {
        /// <summary>
        /// Antenna signal coverage level.
        /// </summary>
        public string AntennaCoverage { get; init; }

        /// <summary>
        /// Name of the mobile network operator.
        /// </summary>
        public string MobileOperator { get; init; }

        /// <summary>
        /// Current status of the modem.
        /// </summary>
        public string ModemStatus { get; init; }

        /// <summary>
        /// Mobile phone number associated with the SIM card.
        /// </summary>
        public string MobileNumber { get; init; }

        public override string ToString()
        {
            return $"SmsModemStatus(AntennaCoverage={AntennaCoverage}, MobileOperator={MobileOperator}, ModemStatus={ModemStatus}, MobileNumber={MobileNumber})";
        }
    }
}
