using System;
using System.Runtime.CompilerServices;
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

    // Routing table shared by container strategies (array/collection, ResourceValue) whose child controls are
    // (re)built AFTER SubscribeToValueChanged has run: each child raises the parameter-changed handler indirectly
    // via RaiseContainerChanged, which looks it up here by the container's main panel - so wiring done at child-
    // creation time works regardless of whether SubscribeToValueChanged has run yet. Weak keys avoid leaking
    // controls and keep multiple container parameters independent; keying by control instance makes one shared
    // table safe across strategy types. Used only on the Avalonia UI thread.
    private static readonly ConditionalWeakTable<Control, EventHandler> containerChangeHandlers = new();

    /// <summary>
    /// Registers the parameter-changed <paramref name="handler"/> against a container's main panel so child
    /// controls (re)built later route their edits back to it via <see cref="RaiseContainerChanged"/>. Container
    /// strategies call this from their <see cref="SubscribeToValueChanged"/> override.
    /// </summary>
    protected static void RegisterContainerChangeHandler(Control mainPanel, EventHandler handler)
        => containerChangeHandlers.AddOrUpdate(mainPanel, handler);

    /// <summary>
    /// Raises the parameter-changed handler registered for <paramref name="mainPanel"/> (if any), always with the
    /// main panel as the sender so the consumer resolves the parameter index from its Name (decision D9).
    /// </summary>
    protected static void RaiseContainerChanged(Control mainPanel)
    {
        if (containerChangeHandlers.TryGetValue(mainPanel, out var handler))
            handler(mainPanel, EventArgs.Empty);
    }

    /// <summary>
    /// Shared <see cref="SubscribeToValueChanged"/> body for container strategies (array/collection,
    /// ResourceValue): the container's main <see cref="StackPanel"/> is the single sync unit, so the handler is
    /// registered against it and child controls (re)built later route their edits back via
    /// <see cref="RaiseContainerChanged"/>. Casts with the standard control-type guard.
    /// </summary>
    protected static void RegisterContainerSubscription(Control control, EventHandler handler)
        => RegisterContainerChangeHandler(RequireControl<StackPanel>(control), handler);

    /// <summary>
    /// Wires the native "value changed" event of a single leaf editor control to <paramref name="onChanged"/>,
    /// passing the control as the sender. This is the one place the editor-type -&gt; change-event mapping lives,
    /// shared by the leaf strategies' <see cref="SubscribeToValueChanged"/> overrides and the ResourceValue payload
    /// editor, so a renamed event or a new editor kind is edited here only (decision D9 keeps the sender choice
    /// with the caller via the <paramref name="onChanged"/> lambda). Unrecognised control types are ignored.
    /// </summary>
    protected static void SubscribeLeafChange(Control control, EventHandler onChanged)
    {
        switch (control)
        {
            case NumericUpDown n: n.ValueChanged += (s, e) => onChanged(control, EventArgs.Empty); break;
            case CheckBox c: c.IsCheckedChanged += (s, e) => onChanged(control, EventArgs.Empty); break;
            case DurationInput d: d.ValueChanged += (s, e) => onChanged(control, EventArgs.Empty); break;
            case TextBox t: t.TextChanged += (s, e) => onChanged(control, EventArgs.Empty); break;
            case DatePicker dp: dp.SelectedDateChanged += (s, e) => onChanged(control, EventArgs.Empty); break;
            case TimePicker tp: tp.SelectedTimeChanged += (s, e) => onChanged(control, EventArgs.Empty); break;
        }
    }

    /// <inheritdoc/>
    public abstract object? ExtractValue(Control control, FieldMetaData field);

    /// <inheritdoc/>
    public abstract void SetValue(Control control, object? value, FieldMetaData field);

    /// <summary>
    /// Default: a leaf strategy renders the field wholesale and delegates to no sub-field controls, so the
    /// filter must not recurse into its sub-types. Container strategies (complex record, array/collection)
    /// override this to return their constituent fields.
    /// </summary>
    public virtual FieldMetaData[] GetRenderedSubFields(FieldMetaData field) => Array.Empty<FieldMetaData>();

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

    /// <summary>
    /// Returns the non-nullable underlying type of a <see cref="Nullable{T}"/> (e.g. <c>int?</c> -&gt; <c>int</c>),
    /// or the type unchanged when it is not nullable. Lets leaf strategies handle both <c>T</c> and <c>T?</c>.
    /// </summary>
    protected static Type UnwrapNullable(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// True when the type is a <see cref="Nullable{T}"/> value type (e.g. <c>int?</c>, <c>bool?</c>). For such
    /// types an empty/unset control extracts as <c>null</c> (the "empty = null" convention, decision D3).
    /// </summary>
    protected static bool IsNullableValueType(Type type) => Nullable.GetUnderlyingType(type) != null;
}
