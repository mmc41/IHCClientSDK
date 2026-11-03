namespace Ihc.Stubbing
{
    /// <summary>
    /// The SMS modem's information, settings and status. The information and status defaults are a real
    /// controller's answers, with its mobile number replaced; no capture holds a settings answer, so the settings
    /// default is a stand-in.
    /// </summary>
    public sealed class StubSmsModem
    {
        private readonly Lock _gate;

        internal StubSmsModem(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>What a modem-info query answers.</summary>
        public SmsModemInfo Info
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new()
        {
            FirmwareVersion = "0.0.0",
            GSMChipVersion = "0",
            HardwareRevision = "0",
            ProductionDate = "2020/20",
            Detected = true,
            SerialNumber = "000000000000",
            IMEINumber = "000000000000000",
        };

        /// <summary>The settings a get answers and a set replaces. The default is a stand-in, not a captured answer:
        /// the vendor clients' logs name the settings read only in WSDL text.</summary>
        public SmsModemSettings Settings
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new()
        {
            PowerupMessage = "",
            PowerdownMessage = "",
            RecipientSlots = "000000000000000000000000000000",
            RelaySMS = false,
            ForceStandAloneMode = false,
            SendLowBatteryNotification = false,
            SendLowBatteryNotificationLanguage = false,
            SendLEDDimmerErrorNotification = false,
        };

        /// <summary>What a modem-status query answers.</summary>
        public SmsModemStatus Status
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new()
        {
            AntennaCoverage = "text.sms_modem.antenna_coverage.medium_high",
            MobileOperator = "Mock",
            ModemStatus = "text.sms_modem.ok",
            MobileNumber = "00000000",
        };
    }
}
