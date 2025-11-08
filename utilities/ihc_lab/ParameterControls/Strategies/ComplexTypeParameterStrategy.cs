using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling complex types (records/classes with properties).
/// Creates nested controls for each property recursively.
/// </summary>
/// <remarks>
/// This is a fallback strategy that should be registered last in the registry.
/// It handles any type that has SubTypes (properties/fields).
/// </remarks>
public class ComplexTypeParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle complex types with sub-properties.
    /// This should be registered last as it's a catch-all for types with SubTypes.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        // Handle any type with sub-types (except files which are handled separately)
        return field.SubTypes.Length > 0 && !field.IsFile;
    }

    /// <summary>
    /// Creates a StackPanel with nested controls for each property.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"ComplexTypeParameterStrategy cannot handle type '{field.Type.FullName}'");

        // Create a horizontal StackPanel to hold all sub-controls
        var stackPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(stackPanel, field.Description);
        }

        var subStrategies = new Dictionary<string, IParameterControlStrategy>();

        // Create controls for each sub-field
        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subControlName = $"{controlName}.{i}";

            // Get strategy for this sub-field
            var registry = ParameterControlRegistry.Instance;
            var subStrategy = registry.GetStrategy(subField);

            // Add label for the sub-field
            var label = new TextBlock
            {
                Text = subField.Name + ":",
                Margin = new Thickness(0, 0, 5, 0),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrWhiteSpace(subField.Description))
            {
                ToolTip.SetTip(label, $"Type: {subField.Type.Name}: {subField.Description}");
            }

            stackPanel.Children.Add(label);

            // Create control for sub-field
            var subResult = subStrategy.CreateControl(subField, subControlName);

            // Set metadata on sub-control so event coordinator can subscribe to it
            subResult.Control.Tag = new OperationSupport.ControlMetadata
            {
                Field = subField,
                Strategy = subStrategy
            };

            stackPanel.Children.Add(subResult.Control);

            // Store strategy for later value extraction
            subStrategies[subControlName] = subStrategy;
        }

        return new ControlCreationResult
        {
            Control = stackPanel,
            IsComposite = true,
            SubStrategies = subStrategies
        };
    }

    /// <summary>
    /// Extracts values from all sub-controls and creates an instance of the complex type.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not StackPanel stackPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        // Extract values from each sub-control
        var subValues = new object?[field.SubTypes.Length];

        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subControlName = $"{stackPanel.Name}.{i}";

            // Find the sub-control by name
            var subControl = FindControlByName(stackPanel, subControlName);
            if (subControl == null)
                throw new InvalidOperationException(
                    $"Could not find sub-control '{subControlName}' in complex type control");

            // Get strategy and extract value
            var registry = ParameterControlRegistry.Instance;
            var subStrategy = registry.GetStrategy(subField);
            subValues[i] = subStrategy.ExtractValue(subControl, subField);
        }

        // Create instance of the complex type using primary constructor
        // Complex types must have a primary constructor that accepts all properties in order
        try
        {
            return Activator.CreateInstance(field.Type, subValues);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of {field.Type.Name} with extracted values. " +
                $"Ensure type has a primary constructor accepting {field.SubTypes.Length} parameters in declaration order: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets values into all sub-controls from a complex type instance.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not StackPanel stackPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        if (value == null)
            return;

        // Set values for each sub-control
        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subControlName = $"{stackPanel.Name}.{i}";

            // Find the sub-control by name
            var subControl = FindControlByName(stackPanel, subControlName);
            if (subControl == null)
                continue;

            // Get the property value from the complex object
            var property = value.GetType().GetProperty(subField.Name);
            if (property == null)
                continue;

            var subValue = property.GetValue(value);

            // Get strategy and set value
            var registry = ParameterControlRegistry.Instance;
            var subStrategy = registry.GetStrategy(subField);
            subStrategy.SetValue(subControl, subValue, subField);
        }
    }

    /// <summary>
    /// Helper method to find a control by name recursively.
    /// </summary>
    private static Control? FindControlByName(Control parent, string name)
    {
        if (parent.Name == name)
            return parent;

        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    var found = FindControlByName(childControl, name);
                    if (found != null)
                        return found;
                }
            }
        }

        return null;
    }
}
