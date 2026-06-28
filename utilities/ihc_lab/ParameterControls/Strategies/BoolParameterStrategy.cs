using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling boolean parameters. A non-nullable <c>bool</c> is rendered as a pair of RadioButtons
/// (true/false); a nullable <c>bool?</c> is rendered as a three-state CheckBox whose indeterminate state means
/// null (the "empty = null" convention, decision D3 - a checkbox cannot otherwise be "empty").
/// </summary>
public class BoolParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle boolean types (including <c>bool?</c>).
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return UnwrapNullable(field.Type) == typeof(bool);
    }

    /// <summary>
    /// Creates a three-state CheckBox for <c>bool?</c>, or a StackPanel of two RadioButtons for <c>bool</c>.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        if (IsNullableValueType(field.Type))
        {
            var checkBox = new CheckBox
            {
                Name = controlName,
                Content = string.IsNullOrWhiteSpace(field.Name) ? "value" : field.Name,
                IsThreeState = true,
                IsChecked = null // indeterminate = null (unset)
            };

            ApplyDescriptionTooltip(checkBox, field);
            return checkBox;
        }

        var stackPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        // Tag carries the boolean meaning structurally so extract/set never depend on the display caption.
        var trueRadio = new RadioButton
        {
            Name = controlName, // Set name so event handler can identify it
            Content = "True",
            GroupName = controlName,
            IsChecked = false,
            Tag = true
        };

        var falseRadio = new RadioButton
        {
            Name = controlName, // Set same name (they represent the same field)
            Content = "False",
            GroupName = controlName,
            IsChecked = true, // Default to false
            Tag = false
        };

        stackPanel.Children.Add(trueRadio);
        stackPanel.Children.Add(falseRadio);

        ApplyDescriptionTooltip(stackPanel, field);

        return stackPanel;
    }

    /// <summary>
    /// Subscribes to the control's change event. For the three-state CheckBox it is IsCheckedChanged; for the
    /// RadioButton pair each radio's IsCheckedChanged is wired, with the owning StackPanel passed as the sender.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is CheckBox checkBox)
        {
            checkBox.IsCheckedChanged += (s, e) => handler(checkBox, EventArgs.Empty);
            return;
        }

        if (control is StackPanel stackPanel)
        {
            foreach (var radioButton in stackPanel.Children.OfType<RadioButton>())
            {
                radioButton.IsCheckedChanged += (s, e) => handler(stackPanel, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Extracts the boolean value. A three-state CheckBox returns its <c>bool?</c> (null when indeterminate);
    /// the RadioButton pair returns a non-nullable bool.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is CheckBox checkBox)
            return checkBox.IsChecked; // bool? : true / false / null (unset)

        var stackPanel = RequireControl<StackPanel>(control);

        // Locate the "true" radio by its Tag rather than its caption.
        var trueRadio = stackPanel.Children
            .OfType<RadioButton>()
            .FirstOrDefault(r => r.Tag is true);

        if (trueRadio == null)
            throw new InvalidOperationException(
                "Could not find True RadioButton in control");

        return trueRadio.IsChecked == true;
    }

    /// <summary>
    /// Sets a boolean value. For the three-state CheckBox, null restores the indeterminate (unset) state.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is CheckBox checkBox)
        {
            checkBox.IsChecked = value as bool?; // null -> indeterminate (unset)
            return;
        }

        var stackPanel = RequireControl<StackPanel>(control);

        bool boolValue = value is bool b ? b : false;

        // Each radio's Tag holds the boolean it represents; the true radio is checked when the value is true.
        foreach (var radio in stackPanel.Children.OfType<RadioButton>())
        {
            if (radio.Tag is bool isTrueRadio)
                radio.IsChecked = isTrueRadio ? boolValue : !boolValue;
        }
    }
}
