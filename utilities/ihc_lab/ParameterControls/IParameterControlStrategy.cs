using System;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls;

/// <summary>
/// Strategy interface for creating and managing Avalonia controls for specific parameter types.
/// Each implementation handles one type family (e.g., strings, booleans, numerics, arrays).
/// </summary>
/// <remarks>
/// Implementations should follow these guidelines:
/// - CanHandle() should be specific and return true only for types this strategy can manage
/// - CreateControl() should set the control.Name property to the provided controlName
/// - CreateControl() should add a tooltip with the field description if available
/// - ExtractValue() should handle null controls gracefully
/// - SetValue() should handle null values gracefully
/// - All methods should be thread-safe if used in concurrent scenarios
/// </remarks>
public interface IParameterControlStrategy
{
    /// <summary>
    /// Determines whether this strategy can handle the specified field type.
    /// </summary>
    /// <param name="field">The field metadata to check</param>
    /// <returns>True if this strategy can create controls for the field type; otherwise false</returns>
    bool CanHandle(FieldMetaData field);

    /// <summary>
    /// Creates an Avalonia control appropriate for editing the field value.
    /// </summary>
    /// <param name="field">The field metadata describing the parameter</param>
    /// <param name="controlName">The name to assign to the created control (for identification)</param>
    /// <returns>A ControlCreationResult containing the created control and metadata</returns>
    /// <exception cref="NotSupportedException">Thrown if the field type is not supported by this strategy</exception>
    ControlCreationResult CreateControl(FieldMetaData field, string controlName);

    /// <summary>
    /// Extracts the current value from a control created by this strategy.
    /// </summary>
    /// <param name="control">The control to extract the value from</param>
    /// <param name="field">The field metadata (for type conversion)</param>
    /// <returns>The value extracted from the control, or null if the control is empty/invalid</returns>
    /// <exception cref="InvalidOperationException">Thrown if the control type doesn't match this strategy</exception>
    object? ExtractValue(Control control, FieldMetaData field);

    /// <summary>
    /// Sets a value into a control created by this strategy.
    /// </summary>
    /// <param name="control">The control to set the value into</param>
    /// <param name="value">The value to set (may be null)</param>
    /// <param name="field">The field metadata (for type conversion)</param>
    /// <exception cref="InvalidOperationException">Thrown if the control type doesn't match this strategy</exception>
    void SetValue(Control control, object? value, FieldMetaData field);
}
