using System;

namespace Ihc
{
    /// <summary>
    /// An IHC Wireless (Airlink) device as the controller reports it in a detection, a test reply or its device list.
    /// </summary>
    public record RFDevice
    {
        /// <summary>
        /// Battery state, a flag rather than a percentage: 1 means OK and 0 low. Meaningful for battery-powered
        /// products only.
        /// </summary>
        public int BatteryLevel { get; init; }

        /// <summary>
        /// Radio signal strength (RSSI), 0-48: 10 and up is OK, 5-9 low and below 5 critical, and 0 means no reading; a
        /// working device should not read below 10.
        /// </summary>
        public int SignalStrength { get; init; }

        /// <summary>
        /// Product type code, the project's <c>device_type</c>: 0x801-0x814 for the wireless products (e.g. 0x802 =
        /// 2050, a 6-key push-button) and 0x880 for the signal-strength test kit.
        /// </summary>
        public int DeviceType { get; init; }

        /// <summary>
        /// Unique 64-bit serial number of the device; a project stores it as the hexadecimal digits of this value.
        /// </summary>
        public long SerialNumber { get; init; }

        /// <summary>
        /// Firmware or hardware version of the device.
        /// </summary>
        public int Version { get; init; }

        /// <summary>
        /// Detection flag: in a detection reply, <c>true</c> means a device was found and <c>false</c> that one was
        /// removed.
        /// </summary>
        public bool Detected { get; init; }

        public override string ToString()
        {
            return $"RFDevice(BatteryLevel={BatteryLevel}, SignalStrength={SignalStrength}, DeviceType={DeviceType}, SerialNumber={SerialNumber}, Version={Version}, Detected={Detected})";
        }
    }
}