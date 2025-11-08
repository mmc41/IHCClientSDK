using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling ResourceValue parameters.
/// Creates controls for ResourceID and ValueKind selection.
/// </summary>
/// <remarks>
/// This is a simplified implementation that handles only the metadata (ResourceID and ValueKind).
/// Full union value editing based on ValueKind can be added in future enhancements.
/// </remarks>
public class ResourceValueParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle ResourceValue types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(ResourceValue);
    }

    /// <summary>
    /// Creates a StackPanel with NumericUpDown for ResourceID and ComboBox for ValueKind.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"ResourceValueParameterStrategy cannot handle type '{field.Type.FullName}'");

        // Create horizontal StackPanel to hold both controls
        var stackPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        // Create NumericUpDown for ResourceID
        var resourceIdUpDown = new NumericUpDown
        {
            Name = $"{controlName}.ResourceID",
            FormatString = "F0",
            Increment = 1,
            MinWidth = 100,
            Value = 0,
            Minimum = 0,
            Maximum = int.MaxValue
        };
        ToolTip.SetTip(resourceIdUpDown, "Resource ID");

        // Create ComboBox for ValueKind
        var valueKindDropDown = new ComboBox
        {
            Name = $"{controlName}.ValueKind",
            MinWidth = 100,
            ItemsSource = Enum.GetNames(typeof(ResourceValue.ValueKind))
        };

        // Select first item by default
        if (valueKindDropDown.ItemsSource != null)
        {
            valueKindDropDown.SelectedIndex = 0;
        }

        ToolTip.SetTip(valueKindDropDown, "Value kind");

        stackPanel.Children.Add(resourceIdUpDown);
        stackPanel.Children.Add(valueKindDropDown);

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(stackPanel, field.Description);
        }

        return new ControlCreationResult
        {
            Control = stackPanel,
            IsComposite = false
        };
    }

    /// <summary>
    /// Extracts ResourceID and ValueKind from controls and creates a ResourceValue instance.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not StackPanel stackPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        // Find ResourceID control
        var resourceIdControl = stackPanel.Children
            .OfType<NumericUpDown>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ResourceID") == true);

        if (resourceIdControl == null)
            throw new InvalidOperationException(
                "Could not find ResourceID NumericUpDown control");

        // Find ValueKind control
        var valueKindControl = stackPanel.Children
            .OfType<ComboBox>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ValueKind") == true);

        if (valueKindControl == null)
            throw new InvalidOperationException(
                "Could not find ValueKind ComboBox control");

        int resourceId = (int)(resourceIdControl.Value ?? 0);

        // Parse ValueKind
        ResourceValue.ValueKind valueKind = ResourceValue.ValueKind.BOOL;
        if (valueKindControl.SelectedItem is string valueKindStr)
        {
            Enum.TryParse(valueKindStr, out valueKind);
        }

        // Create a basic ResourceValue with the specified ID and kind
        // Note: This creates an empty union value - full value editing can be added later
        return new ResourceValue
        {
            ResourceID = resourceId,
            IsValueRuntime = true,
            Value = new ResourceValue.UnionValue
            {
                ValueKind = valueKind
            }
        };
    }

    /// <summary>
    /// Sets a ResourceValue into the controls.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not StackPanel stackPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        if (value is not ResourceValue resourceValue)
        {
            // Set defaults
            SetDefaultValues(stackPanel);
            return;
        }

        // Find ResourceID control
        var resourceIdControl = stackPanel.Children
            .OfType<NumericUpDown>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ResourceID") == true);

        if (resourceIdControl != null)
        {
            resourceIdControl.Value = resourceValue.ResourceID;
        }

        // Find ValueKind control
        var valueKindControl = stackPanel.Children
            .OfType<ComboBox>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ValueKind") == true);

        if (valueKindControl != null && valueKindControl.ItemsSource != null)
        {
            string valueKindName = resourceValue.Value.ValueKind.ToString();
            var items = valueKindControl.ItemsSource.Cast<string>().ToList();
            int index = items.IndexOf(valueKindName);

            if (index >= 0)
            {
                valueKindControl.SelectedIndex = index;
            }
        }
    }

    /// <summary>
    /// Sets default values into the controls.
    /// </summary>
    private static void SetDefaultValues(StackPanel stackPanel)
    {
        // Find ResourceID control and set to 0
        var resourceIdControl = stackPanel.Children
            .OfType<NumericUpDown>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ResourceID") == true);

        if (resourceIdControl != null)
        {
            resourceIdControl.Value = 0;
        }

        // Find ValueKind control and select first item
        var valueKindControl = stackPanel.Children
            .OfType<ComboBox>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ValueKind") == true);

        if (valueKindControl != null)
        {
            valueKindControl.SelectedIndex = 0;
        }
    }
}
