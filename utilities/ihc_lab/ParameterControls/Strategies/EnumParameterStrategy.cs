using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling enum parameters.
/// Creates a ComboBox control with enum values.
/// </summary>
public class EnumParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle enum types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return field.Type.IsEnum;
    }

    /// <summary>
    /// Creates a ComboBox control populated with enum values.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        var comboBox = new ComboBox
        {
            Name = controlName,
            MinWidth = 150
        };

        // Get all enum values and wrap them in EnumItem for display
        var enumItems = Enum.GetValues(field.Type)
            .Cast<object>()
            .Select(e => new EnumItem(e))
            .ToArray();

        comboBox.ItemsSource = enumItems;
        comboBox.DisplayMemberBinding = new Binding(nameof(EnumItem.DisplayName));

        // Select first item by default
        if (enumItems.Length > 0)
        {
            comboBox.SelectedIndex = 0;
        }

        ApplyDescriptionTooltip(comboBox, field);

        return comboBox;
    }

    /// <summary>
    /// Subscribes to the ComboBox's SelectionChanged event.
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        if (control is ComboBox comboBox)
            comboBox.SelectionChanged += (s, e) => handler(comboBox, EventArgs.Empty);
    }

    /// <summary>
    /// Extracts the selected enum value from a ComboBox control.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var comboBox = RequireControl<ComboBox>(control);

        if (comboBox.SelectedItem is EnumItem enumItem)
        {
            return enumItem.Value;
        }

        // If nothing selected, return the first enum value as default
        if (field.Type.IsEnum)
        {
            var values = Enum.GetValues(field.Type);
            if (values.Length > 0)
                return values.GetValue(0);
        }

        return null;
    }

    /// <summary>
    /// Sets an enum value into a ComboBox control.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var comboBox = RequireControl<ComboBox>(control);

        if (value == null)
        {
            comboBox.SelectedIndex = 0;
            return;
        }

        // Find the matching EnumItem
        if (comboBox.ItemsSource != null)
        {
            var items = comboBox.ItemsSource.Cast<EnumItem>().ToList();
            var matchingItem = items.FirstOrDefault(item => Equals(item.Value, value));

            if (matchingItem != null)
            {
                comboBox.SelectedItem = matchingItem;
            }
            else
            {
                // Value not found, select first item
                comboBox.SelectedIndex = 0;
            }
        }
    }

    /// <summary>
    /// Helper class to wrap enum values with display names for ComboBox binding.
    /// </summary>
    private class EnumItem
    {
        public object Value { get; }
        public string DisplayName { get; }

        public EnumItem(object enumValue)
        {
            Value = enumValue;
            DisplayName = enumValue.ToString() ?? string.Empty;
        }
    }
}
