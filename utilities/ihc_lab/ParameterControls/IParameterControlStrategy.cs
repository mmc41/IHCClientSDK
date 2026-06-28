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
/// - SubscribeToValueChanged() should wire the control's own change event(s) so the strategy
///   (not the caller) owns the knowledge of which event signals an edit
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
    /// <returns>The created control</returns>
    /// <exception cref="NotSupportedException">Thrown if the field type is not supported by this strategy</exception>
    Control CreateControl(FieldMetaData field, string controlName);

    /// <summary>
    /// Subscribes the given handler to the value-changed event(s) of a control created by this strategy.
    /// Each strategy knows which event on its own control signals an edit, so this keeps that knowledge
    /// next to <see cref="CreateControl"/> rather than in a central type switch. Implementations that have
    /// no editable leaf event (e.g. complex containers whose sub-controls are subscribed individually) may
    /// leave this as a no-op.
    /// </summary>
    /// <param name="control">The control to subscribe to</param>
    /// <param name="handler">The handler to invoke when the control's value changes</param>
    void SubscribeToValueChanged(Control control, EventHandler handler);

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

    /// <summary>
    /// Returns the sub-fields whose controls this strategy builds via the registry, so the operation filter can
    /// recurse into them to decide renderability. Container strategies return their constituent fields (a complex
    /// record's properties, a collection's element); leaf strategies that render a field wholesale (scalars,
    /// files, the ResourceValue editor) return an empty array.
    /// </summary>
    /// <param name="field">The field metadata describing the parameter.</param>
    /// <returns>The sub-fields rendered via the registry, or an empty array for a leaf-rendered field.</returns>
    FieldMetaData[] GetRenderedSubFields(FieldMetaData field);
}
