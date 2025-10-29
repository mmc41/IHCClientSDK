using System;

namespace Ihc.App
{
    /// <summary>
    /// Represents non-editable information from the IHC controller administrator interface.
    /// This model captures read-only system information shown in the primary status page.
    /// Reuses existing models from Ihc namespace where applicable.
    /// </summary>
    public record InformationModel
    {
        /// <summary>
        /// Time the controller has been running since last boot (LK IHC® controller oppetid).
        /// Available from OpenAPIService.GetUptime().
        /// </summary>
        public TimeSpan Uptime { get; init; }

        /// <summary>
        /// Current date and time on the IHC controller (Klokken på LK IHC® controlleren).
        /// Available from OpenAPIService.GetTime().
        /// </summary>
        public DateTimeOffset ControllerTime { get; init; }

        /// <summary>
        /// Controller serial number (Serie nummer).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public string SerialNumber { get; init; }

        /// <summary>
        /// Production date of the controller (Produktions dato).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public string ProductionDate { get; init; }

        /// <summary>
        /// Controller software/firmware version (Software version).
        /// Available from OpenAPIService.GetFWVersion().
        /// </summary>
        public FWVersion SoftwareVersion { get; init; }

        /// <summary>
        /// Controller hardware version (Hardware version).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public string HardwareVersion { get; init; }

        /// <summary>
        /// Input/Output module version (IO version).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public string IoVersion { get; init; }

        /// <summary>
        /// Radio Frequency module version (RF version).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public string RfVersion { get; init; }

        /// <summary>
        /// Radio Frequency serial number (RF serie nummer).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public string RfSerialNumber { get; init; }

        /// <summary>
        /// SMS modem firmware version (LK IHC SMS Modem).
        /// Available from SmsModemService.GetSmsModemInfo().FirmwareVersion.
        /// </summary>
        public string SmsModemVersion { get; init; }

        /// <summary>
        /// Date of the software release (Software dato).
        /// Note: Not directly available via API in current implementation.
        /// </summary>
        public DateTimeOffset SoftwareDate { get; init; }

        /// <summary>
        /// Current operational status of the controller (LK IHC® controller status).
        /// Available from ControllerService.GetControllerState().
        /// </summary>
        public ControllerState? ControllerStatus { get; init; }

        /// <summary>
        /// SD card storage information including used and total space (SD kort).
        /// Available from ControllerService.GetSDCardInfo().
        /// </summary>
        public SDInfo SdCard { get; init; }

        public override string ToString()
        {
            return $"InformationModel(Uptime={Uptime}, ControllerTime={ControllerTime}, SerialNumber={SerialNumber}, " +
                   $"ProductionDate={ProductionDate}, SoftwareVersion={SoftwareVersion}, HardwareVersion={HardwareVersion}, " +
                   $"IoVersion={IoVersion}, RfVersion={RfVersion}, RfSerialNumber={RfSerialNumber}, " +
                   $"SmsModemVersion={SmsModemVersion}, SoftwareDate={SoftwareDate}, ControllerStatus={ControllerStatus}, " +
                   $"SdCard={SdCard})";
        }
    }
}