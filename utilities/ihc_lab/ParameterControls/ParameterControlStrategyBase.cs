using System;
using Avalonia.Controls;
using Ihc;

namespace IhcLab.ParameterControls;

/// <summary>
/// Base class for <see cref="IParameterControlStrategy"/> implementations that centralises the
/// scaffolding every strategy would otherwise repeat: the <see cref="CanHandle"/> guard at the top
/// of <see cref="CreateControl"/>, the description tooltip, the "wrong control type" cast guard, and
/// the default no-op value-changed subscription.
/// </summary>
public abstract class ParameterControlStrategyBase : IParameterControlStrategy
{
    /// <inheritdoc/>
    public abstract bool CanHandle(FieldMetaData field);

    /// <inheritdoc/>
    public abstract Control CreateControl(FieldMetaData field, string controlName);

    /// <summary>
    /// Default no-op subscription. Strategies whose control has no single leaf value-changed event
    /// (complex containers, file pickers, deferred types) inherit this; leaf strategies override it
    /// to wire their control's own change event.
    /// </summary>
    public virtual void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        // Intentionally a no-op for container/leafless strategies; see method summary.
    }

    /// <inheritdoc/>
    public abstract object? ExtractValue(Control control, FieldMetaData field);

    /// <inheritdoc/>
    public abstract void SetValue(Control control, object? value, FieldMetaData field);

    /// <summary>
    /// Throws <see cref="NotSupportedException"/> when this strategy cannot handle the given field.
    /// Call at the top of <see cref="CreateControl"/>.
    /// </summary>
    protected void EnsureCanHandle(FieldMetaData field)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"{GetType().Name} cannot handle type '{field.Type.FullName}'");
    }

    /// <summary>
    /// Adds the field description as a tooltip on the control when a description is present.
    /// </summary>
    protected static void ApplyDescriptionTooltip(Control control, FieldMetaData field)
    {
        if (!string.IsNullOrWhiteSpace(field.Description))
            ToolTip.SetTip(control, field.Description);
    }

    /// <summary>
    /// Casts a control created by this strategy to its expected type, throwing a consistent
    /// <see cref="InvalidOperationException"/> when it is the wrong type.
    /// </summary>
    protected static T RequireControl<T>(Control control) where T : Control
    {
        return control as T
            ?? throw new InvalidOperationException(
                $"Expected {typeof(T).Name} control but got {control.GetType().Name}");
    }
}
