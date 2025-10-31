using System;

namespace Ihc.App
{
    /// <summary>
    /// Represents non-editable status information from the IHC controller administrator interface.
    /// Reuses existing models from Ihc namespace.
    /// </summary>
    public record InformationModel
    {
        /// <summary>
        /// Time the controller has been running since last boot.
        /// </summary>
        public TimeSpan Uptime { get; init; }

        /// <summary>
        /// Current date and time on the IHC controller.
        /// </summary>
        public DateTimeOffset ControllerTime { get; init; }

        /// <summary>
        /// Controller serial number.
        /// </summary>
        public string SerialNumber { get; init; }

        /// <summary>
        /// Production date of the controller
        /// </summary>
        public string ProductionDate { get; init; }

        /// <summary>
        /// Controller software/firmware version
        /// </summary>
        public string SoftwareVersion { get; init; }

        /// <summary>
        /// Controller hardware version
        /// </summary>
        public string HardwareVersion { get; init; }

        /// <summary>
        /// Input/Output module version
        /// </summary>
        public string IoVersion { get; init; }

        /// <summary>
        /// Radio Frequency module version
        /// </summary>
        public string RfVersion { get; init; }

        /// <summary>
        /// Radio Frequency serial number
        /// </summary>
        public string RfSerialNumber { get; init; }

        /// <summary>
        /// SMS modem firmware version
        /// </summary>
        public string SmsModemVersion { get; init; }

        /// <summary>
        /// Date of the software release
        /// </summary>
        public DateTimeOffset SoftwareDate { get; init; }

        /// <summary>
        /// Current operational status of the controller. 
        /// </summary>
        public ControllerState ControllerStatus { get; init; }

        /// <summary>
        /// SD card storage information including used and total space (SD kort). 
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