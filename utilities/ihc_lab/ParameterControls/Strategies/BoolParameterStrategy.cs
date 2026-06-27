using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling boolean parameters.
/// Creates a pair of RadioButton controls for true/false selection.
/// </summary>
public class BoolParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle boolean types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(bool);
    }

    /// <summary>
    /// Creates a StackPanel with two RadioButton controls for true/false selection.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

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
    /// Subscribes to each RadioButton's IsCheckedChanged event. The owning StackPanel (which carries the
    /// field metadata) is passed as the sender so the handler can locate the parameter, mirroring how the
    /// control is identified elsewhere.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is not StackPanel stackPanel)
            return;

        foreach (var radioButton in stackPanel.Children.OfType<RadioButton>())
        {
            radioButton.IsCheckedChanged += (s, e) => handler(stackPanel, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Extracts the boolean value from the RadioButton controls.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
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
    /// Sets a boolean value into the RadioButton controls.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
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
