using System;

/// <summary>
/// Represents an RF (Radio Frequency) device in the Airlink system.
/// </summary>
public record RFDevice
{
    /// <summary>
    /// Battery level of the device (0-100).
    /// </summary>
    public int BatteryLevel { get; init; }

    /// <summary>
    /// Signal strength of the device.
    /// </summary>
    public int SignalStrength { get; init; }

    /// <summary>
    /// Type identifier of the device.
    /// </summary>
    public int DeviceType { get; init; }

    /// <summary>
    /// Unique serial number of the device.
    /// </summary>
    public long SerialNumber { get; init; }

    /// <summary>
    /// Firmware or hardware version of the device.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Indicates whether the device has been detected.
    /// </summary>
    public bool Detected { get; init; }
}