namespace Ihc
{
    /// <summary>
    /// An RS-485 LED Dimmer 2 entry as the controller reports it: one entry per channel, a dimmer's two channels as
    /// consecutive entries.
    /// </summary>
    public record LedDimmerInfo
    {
        /// <summary>
        /// The channel's location text, from the project.
        /// </summary>
        public string? Location { get; init; }

        /// <summary>
        /// Channel index within the dimmer, 0 or 1.
        /// </summary>
        public sbyte Channel { get; init; }

        /// <summary>
        /// Bootloader firmware version.
        /// </summary>
        public string? BootloaderVersion { get; init; }

        /// <summary>
        /// The dimmer's firmware (application) version; releases are named like <c>DAP.T.01.01.40</c>.
        /// </summary>
        public string? ApplicationVersion { get; init; }

        /// <summary>
        /// Status code: -1 means offline; the meaning of other values is not documented.
        /// </summary>
        public sbyte ApplicationStatus { get; init; }

        /// <summary>
        /// Hardware version.
        /// </summary>
        public string? HardwareVersion { get; init; }

        /// <summary>
        /// The dimmer's serial number, a string that both of its channel entries carry.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// The channel's current light level, a raw signed byte whose scale is not documented.
        /// </summary>
        public sbyte Level { get; init; }

        /// <summary>
        /// Fault bits: 0x80 overcurrent, 0x40 overvoltage, 0x20 overheating, 0x10 load fault (0x80 makes the signed
        /// byte negative); the low four bits are not documented, so ignore them.
        /// </summary>
        public sbyte ErrorFlags { get; init; }

        /// <summary>
        /// Bus id the channel was linked with, 0 when not linked; a linked id is 0x80-0xBF, which this signed byte
        /// holds as -128 to -65.
        /// </summary>
        public sbyte ChannelID { get; init; }

        public override string ToString()
        {
            return $"LedDimmerInfo(Location={Location}, Channel={Channel}, BootloaderVersion={BootloaderVersion}, ApplicationVersion={ApplicationVersion}, ApplicationStatus={ApplicationStatus}, HardwareVersion={HardwareVersion}, SerialNumber={SerialNumber}, Level={Level}, ErrorFlags={ErrorFlags}, ChannelID={ChannelID})";
        }
    }

    /// <summary>
    /// Light level and fault flags of one LED-dimmer channel, as
    /// <see cref="ILedDimmerManagementService.GetLightLevel"/> returns them.
    /// </summary>
    public record LedDimmerLevel
    {
        /// <summary>
        /// Current light level as a raw signed byte, probably encoded as in <see cref="LedDimmerInfo.Level"/>.
        /// </summary>
        public sbyte Level { get; init; }

        /// <summary>
        /// Fault bits, probably laid out as in <see cref="LedDimmerInfo.ErrorFlags"/> (0x80 overcurrent, 0x40
        /// overvoltage, 0x20 overheating, 0x10 load fault).
        /// </summary>
        public sbyte ErrorFlags { get; init; }

        public override string ToString()
        {
            return $"LedDimmerLevel(Level={Level}, ErrorFlags={ErrorFlags})";
        }
    }

    /// <summary>
    /// State of the current or last LED-dimmer firmware upgrade, as
    /// <see cref="ILedDimmerManagementService.GetFirmwareUpgradeProgress"/> reports it.
    /// </summary>
    public record LedDimmerProgress
    {
        /// <summary>
        /// The controller's message, at least for a failed upgrade, where it gives the failure reason.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Serial number of the dimmer being upgraded; an empty or <c>null</c> value stands for all dimmers.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// Upgrade status: <c>running</c>, <c>finished</c> or <c>failed</c>, in lower case; the upgrade is still in
        /// progress while the status is <c>running</c>.
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
        /// The status value for a running upgrade, <c>running</c>.
        /// </summary>
        public string? Running { get; init; }

        /// <summary>
        /// The status value for a finished upgrade, <c>finished</c>.
        /// </summary>
        public string? Finished { get; init; }

        /// <summary>
        /// The status value for a failed upgrade, <c>failed</c>.
        /// </summary>
        public string? Failed { get; init; }

        public override string ToString()
        {
            return $"LedDimmerProgress(Message={Message}, SerialNumber={SerialNumber}, Status={Status}, Progress={Progress}, Maximum={Maximum}, Running={Running}, Finished={Finished}, Failed={Failed})";
        }
    }
}
