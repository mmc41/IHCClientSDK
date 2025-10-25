namespace Ihc
{
    public record SmsModemSettings
    {
        public string PowerupMessage { get; init; }

        public string PowerdownMessage { get; init; }

        public string PowerdownNumber { get; init; }

        public bool RelaySMS { get; init; }

        public bool ForceStandAloneMode { get; init; }

        public bool SendLowBatteryNotification { get; init; }

        public bool SendLowBatteryNotificationLanguage { get; init; }

        public bool SendLEDDimmerErrorNotification { get; init; }

        public override string ToString()
        {
            return $"SmsModemSettings(PowerupMessage={PowerupMessage}, PowerdownMessage={PowerdownMessage}, PowerdownNumber={PowerdownNumber}, RelaySMS={RelaySMS}, ForceStandAloneMode={ForceStandAloneMode}, SendLowBatteryNotification={SendLowBatteryNotification}, SendLowBatteryNotificationLanguage={SendLowBatteryNotificationLanguage}, SendLEDDimmerErrorNotification={SendLEDDimmerErrorNotification})";
        }
    }

    public record SmsModemInfo
    {
        public string FirmwareVersion { get; init; }

        public string GSMChipVersion { get; init; }

        public string HardwareRevision { get; init; }

        public string ProductionDate { get; init; }

        public bool Detected { get; init; }

        public string SerialNumber { get; init; }

        public string IMEINumber { get; init; }

        public override string ToString()
        {
            return $"SmsModemInfo(FirmwareVersion={FirmwareVersion}, GSMChipVersion={GSMChipVersion}, HardwareRevision={HardwareRevision}, ProductionDate={ProductionDate}, Detected={Detected}, SerialNumber={SerialNumber}, IMEINumber={IMEINumber})";
        }
    }

    public record SmsModemStatus
    {
        public string AntennaCoverage { get; init; }

        public string MobileOperator { get; init; }

        public string ModemStatus { get; init; }

        public string MobileNumber { get; init; }

        public override string ToString()
        {
            return $"SmsModemStatus(AntennaCoverage={AntennaCoverage}, MobileOperator={MobileOperator}, ModemStatus={ModemStatus}, MobileNumber={MobileNumber})";
        }
    }
}
