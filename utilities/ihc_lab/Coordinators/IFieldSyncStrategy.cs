using System;
using Avalonia.Controls;
using IhcLab;

namespace Ihc.App;

/// <summary>
/// Strategy interface for synchronizing values between GUI controls and service parameters.
/// Different implementations handle simple types vs complex types.
/// </summary>
public interface IFieldSyncStrategy
{
    /// <summary>
    /// Determines if this strategy can process the given field type.
    /// </summary>
    /// <param name="field">Field metadata to check.</param>
    /// <returns>True if this strategy supports the field type, false otherwise.</returns>
    bool CanHandle(FieldMetaData field);

    /// <summary>
    /// Syncs a value FROM GUI TO service (GUI → Service direction).
    /// Extracts the current value from DynField controls.
    /// </summary>
    /// <param name="parent">Panel containing the DynField controls.</param>
    /// <param name="field">Field metadata describing the parameter structure.</param>
    /// <param name="indexPath">Index path for finding the DynField.</param>
    /// <returns>The extracted value from the GUI control.</returns>
    object? ExtractValueFromGui(Panel parent, FieldMetaData field, string indexPath);

    /// <summary>
    /// Syncs a value FROM service TO GUI (Service → GUI direction).
    /// Updates DynField controls with the provided value.
    /// </summary>
    /// <param name="parent">Panel containing the DynField controls.</param>
    /// <param name="field">Field metadata describing the parameter structure.</param>
    /// <param name="value">Value to set in the GUI controls.</param>
    /// <param name="indexPath">Index path for finding the DynField.</param>
    void SetValueInGui(Panel parent, FieldMetaData field, object? value, string indexPath);
}
