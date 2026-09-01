namespace Ihc
{
    /// <summary>
    /// High level model of an LED dimmer device without soap distractions.
    /// </summary>
    public record LedDimmerInfo
    {
        /// <summary>
        /// Physical location/name of the device.
        /// </summary>
        public string? Location { get; init; }

        /// <summary>
        /// Dataline channel the device is connected to.
        /// </summary>
        public sbyte Channel { get; init; }

        /// <summary>
        /// Bootloader firmware version.
        /// </summary>
        public string? BootloaderVersion { get; init; }

        /// <summary>
        /// Application firmware version.
        /// </summary>
        public string? ApplicationVersion { get; init; }

        /// <summary>
        /// Application status code.
        /// </summary>
        public sbyte ApplicationStatus { get; init; }

        /// <summary>
        /// Hardware version.
        /// </summary>
        public string? HardwareVersion { get; init; }

        /// <summary>
        /// Serial number of the device.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// Current light level (0-100).
        /// </summary>
        public sbyte Level { get; init; }

        /// <summary>
        /// Error flags bitmask reported by the device.
        /// </summary>
        public sbyte ErrorFlags { get; init; }

        /// <summary>
        /// Assigned channel ID of the device.
        /// </summary>
        public sbyte ChannelID { get; init; }

        public override string ToString()
        {
            return $"LedDimmerInfo(Location={Location}, Channel={Channel}, BootloaderVersion={BootloaderVersion}, ApplicationVersion={ApplicationVersion}, ApplicationStatus={ApplicationStatus}, HardwareVersion={HardwareVersion}, SerialNumber={SerialNumber}, Level={Level}, ErrorFlags={ErrorFlags}, ChannelID={ChannelID})";
        }
    }

    /// <summary>
    /// High level model of an LED dimmer light level reading without soap distractions.
    /// </summary>
    public record LedDimmerLevel
    {
        /// <summary>
        /// Current light level (0-100).
        /// </summary>
        public sbyte Level { get; init; }

        /// <summary>
        /// Error flags bitmask reported by the device.
        /// </summary>
        public sbyte ErrorFlags { get; init; }

        public override string ToString()
        {
            return $"LedDimmerLevel(Level={Level}, ErrorFlags={ErrorFlags})";
        }
    }

    /// <summary>
    /// High level model of LED dimmer firmware upgrade progress without soap distractions.
    /// </summary>
    public record LedDimmerProgress
    {
        /// <summary>
        /// Human readable progress message.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Serial number of the device being upgraded.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// Current upgrade status (e.g. one of the Running/Finished/Failed labels).
        /// </summary>
        public string? Status { get; init; }

        /// <summary>
        /// Current progress value (out of <see cref="Maximum"/>).
        /// </summary>
        public int Progress { get; init; }

        /// <summary>
        /// Maximum progress value corresponding to completion.
        /// </summary>
        public int Maximum { get; init; }

        /// <summary>
        /// Status label used for a running upgrade.
        /// </summary>
        public string? Running { get; init; }

        /// <summary>
        /// Status label used for a finished upgrade.
        /// </summary>
        public string? Finished { get; init; }

        /// <summary>
        /// Status label used for a failed upgrade.
        /// </summary>
        public string? Failed { get; init; }

        public override string ToString()
        {
            return $"LedDimmerProgress(Message={Message}, SerialNumber={SerialNumber}, Status={Status}, Progress={Progress}, Maximum={Maximum}, Running={Running}, Finished={Finished}, Failed={Failed})";
        }
    }
}
