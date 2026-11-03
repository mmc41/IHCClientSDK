using System.ComponentModel.DataAnnotations;

namespace Ihc
{
    /// <summary>
    /// The SMS modem settings the controller holds.
    /// </summary>
    public record SmsModemSettings
    {
        /// <summary>
        /// SMS text sent when the modem starts up on power-up; at most 60 characters by the vendor clients, which the
        /// SDK does not enforce because a controller may hold a longer text that IHC Administrator itself resends.
        /// </summary>
        public string? PowerupMessage { get; init; }

        /// <summary>
        /// SMS text sent at a power failure; at most 60 characters by the vendor clients, which the SDK does not
        /// enforce, as for <see cref="PowerupMessage"/>.
        /// </summary>
        public string? PowerdownMessage { get; init; }

        /// <summary>
        /// The recipient slots: a mask of 30 characters <c>0</c> or <c>1</c>, one per slot of the modem's phone list
        /// in the project (the first for slot 1). A <c>1</c> marks a slot that receives the power-up, power-down and
        /// forwarded texts. A write refuses any other mask, since IHC Administrator builds exactly 30 and ignores any
        /// other length on read; <c>null</c> sends none. The wire calls it <c>powerdownNumber</c>, though it is no
        /// phone number.
        /// </summary>
        // The length is its own rule because a regular expression attribute passes an empty string unchecked.
        [StringLength(30, MinimumLength = 30, ErrorMessage = "RecipientSlots must be 30 characters.")]
        [RegularExpression("^[01]*$", ErrorMessage = "RecipientSlots must hold only the characters 0 and 1.")]
        public string? RecipientSlots { get; init; }

        /// <summary>
        /// Indicates whether an SMS refused for its unauthorized sender is forwarded to the recipient; such messages
        /// are also logged.
        /// </summary>
        public bool RelaySMS { get; init; }

        /// <summary>
        /// Forces the modem to operate in standalone mode, in which the controller can no longer communicate with it;
        /// the controller is then to be switched off, the data cable removed and the system restarted.
        /// </summary>
        public bool ForceStandAloneMode { get; init; }

        /// <summary>
        /// Indicates whether low battery notifications should be sent.
        /// </summary>
        public bool SendLowBatteryNotification { get; init; }

        /// <summary>
        /// Undocumented; probably picks Danish or English for the low-battery notification. Leave it <c>false</c>.
        /// </summary>
        public bool SendLowBatteryNotificationLanguage { get; init; }

        /// <summary>
        /// Indicates whether LED dimmer error notifications should be sent.
        /// </summary>
        public bool SendLEDDimmerErrorNotification { get; init; }

        public override string ToString()
        {
            return $"SmsModemSettings(PowerupMessage={PowerupMessage}, PowerdownMessage={PowerdownMessage}, RecipientSlots={RecipientSlots}, RelaySMS={RelaySMS}, ForceStandAloneMode={ForceStandAloneMode}, SendLowBatteryNotification={SendLowBatteryNotification}, SendLowBatteryNotificationLanguage={SendLowBatteryNotificationLanguage}, SendLEDDimmerErrorNotification={SendLEDDimmerErrorNotification})";
        }
    }

    /// <summary>
    /// The SMS modem's identity, and whether the controller reports a modem present.
    /// </summary>
    public record SmsModemInfo
    {
        /// <summary>
        /// Firmware version of the SMS modem; <see cref="SystemInfo.SmsModemSoftwareVersion"/> need not be this, as it
        /// is reported even without a modem.
        /// </summary>
        public string? FirmwareVersion { get; init; }

        /// <summary>
        /// Version of the GSM chip in the modem.
        /// </summary>
        public string? GSMChipVersion { get; init; }

        /// <summary>
        /// Hardware revision of the modem.
        /// </summary>
        public string? HardwareRevision { get; init; }

        /// <summary>
        /// Production date of the modem.
        /// </summary>
        public string? ProductionDate { get; init; }

        /// <summary>
        /// Indicates whether the controller reports the modem present.
        /// </summary>
        public bool Detected { get; init; }

        /// <summary>
        /// Serial number of the modem.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// IMEI number of the modem (International Mobile Equipment Identity).
        /// </summary>
        public string? IMEINumber { get; init; }

        public override string ToString()
        {
            return $"SmsModemInfo(FirmwareVersion={FirmwareVersion}, GSMChipVersion={GSMChipVersion}, HardwareRevision={HardwareRevision}, ProductionDate={ProductionDate}, Detected={Detected}, SerialNumber={SerialNumber}, IMEINumber={IMEINumber})";
        }
    }

    /// <summary>
    /// The SMS modem's live GSM status.
    /// </summary>
    public record SmsModemStatus
    {
        /// <summary>
        /// Antenna signal coverage level, reported as <c>text.sms_modem.antenna_coverage.low</c> to <c>.high</c> in
        /// five steps, or <c>.not_applicable</c>.
        /// </summary>
        public string? AntennaCoverage { get; init; }

        /// <summary>
        /// Name of the operator of the GSM network the SIM card uses.
        /// </summary>
        public string? MobileOperator { get; init; }

        /// <summary>
        /// Current status of the modem, its SIM and the GSM network, reported as a key such as <c>text.sms_modem.ok</c>
        /// or <c>text.sms_modem.sim_card.no_card</c>.
        /// </summary>
        public string? ModemStatus { get; init; }

        /// <summary>
        /// Mobile phone number of the SIM card; the operator may not make it available.
        /// </summary>
        public string? MobileNumber { get; init; }

        public override string ToString()
        {
            return $"SmsModemStatus(AntennaCoverage={AntennaCoverage}, MobileOperator={MobileOperator}, ModemStatus={ModemStatus}, MobileNumber={MobileNumber})";
        }
    }
}
