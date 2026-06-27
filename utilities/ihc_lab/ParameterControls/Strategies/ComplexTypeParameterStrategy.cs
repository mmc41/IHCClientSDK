using System;
using System.Reflection;
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
public class ComplexTypeParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle complex types with sub-properties.
    /// This should be registered last as it's a catch-all for types with SubTypes.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        // Handle any type with sub-types (except files which are handled separately)
        return field.SubTypes.Length > 0 && !field.IsFile;
    }

    /// <summary>
    /// Creates a StackPanel with nested controls for each property.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

        // Create a horizontal StackPanel to hold all sub-controls
        var stackPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        ApplyDescriptionTooltip(stackPanel, field);

        // Create controls for each sub-field
        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subControlName = $"{controlName}.{i}";

            // Get strategy for this sub-field
            var subStrategy = ParameterControlRegistry.Instance.GetStrategy(subField);

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
            var subControl = subStrategy.CreateControl(subField, subControlName);

            // Set metadata on sub-control so event coordinator can subscribe to it
            subControl.Tag = new OperationSupport.ControlMetadata
            {
                Field = subField,
                Strategy = subStrategy
            };

            stackPanel.Children.Add(subControl);
        }

        return stackPanel;
    }

    // SubscribeToValueChanged: complex types have no leaf value-changed event of their own; each sub-control
    // carries its own strategy metadata and is subscribed individually as the event coordinator recurses, so
    // the base no-op is inherited.

    /// <summary>
    /// Extracts values from all sub-controls and creates an instance of the complex type.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var stackPanel = RequireControl<StackPanel>(control);

        // Extract values from each sub-control
        var subValues = new object?[field.SubTypes.Length];

        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subControlName = $"{stackPanel.Name}.{i}";

            // Find the sub-control by name
            var subControl = OperationSupport.FindControlByName(stackPanel, subControlName);
            if (subControl == null)
                throw new InvalidOperationException(
                    $"Could not find sub-control '{subControlName}' in complex type control");

            // Reuse the strategy captured on the sub-control during CreateControl; fall back to the registry.
            var subStrategy = (subControl.Tag as OperationSupport.ControlMetadata)?.Strategy
                ?? ParameterControlRegistry.Instance.GetStrategy(subField);
            subValues[i] = subStrategy.ExtractValue(subControl, subField);
        }

        // Construct the complex type from the extracted sub-values. Two record shapes are supported:
        //  1. positional/primary-constructor records whose constructor parameter count matches the sub-value
        //     count -> construct positionally; and
        //  2. property-only records (e.g. IhcUser, SmsModemSettings, NetworkSettings, TimeManagerSettings) that
        //     expose only a parameterless constructor -> construct empty, then assign each value to its property
        //     by name. This by-name path is the norm for the high-level models.
        // By-name assignment is also more robust than positional binding, which silently relies on the
        // GetProperties() order matching the constructor order. Reflection SetValue works on init setters.
        try
        {
            // Prefer by-name construction (assign each value to its property by name) whenever the type can be
            // created empty - i.e. it is a value type or exposes a parameterless constructor. By-name is robust
            // against constructor parameter ordering; positional construction is used only as a fallback for
            // types WITHOUT a parameterless constructor (e.g. positional records), where the compiler
            // guarantees the constructor parameter order matches the property declaration order behind subValues.
            bool canConstructEmpty = field.Type.IsValueType
                || field.Type.GetConstructor(Type.EmptyTypes) != null;

            if (!canConstructEmpty)
                return Activator.CreateInstance(field.Type, subValues);

            var instance = Activator.CreateInstance(field.Type);
            for (int i = 0; i < field.SubTypes.Length; i++)
            {
                var subField = field.SubTypes[i];
                var property = subField.AttributeProvider as PropertyInfo
                    ?? field.Type.GetProperty(subField.Name);
                if (property != null && property.CanWrite)
                    property.SetValue(instance, subValues[i]);
            }
            return instance;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of {field.Type.Name} with extracted values. Ensure the type has " +
                $"either a matching positional constructor accepting {field.SubTypes.Length} parameters in " +
                $"declaration order, or a parameterless constructor plus settable properties: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets values into all sub-controls from a complex type instance.
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var stackPanel = RequireControl<StackPanel>(control);

        if (value == null)
            return;

        // Set values for each sub-control
        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subControlName = $"{stackPanel.Name}.{i}";

            // Find the sub-control by name
            var subControl = OperationSupport.FindControlByName(stackPanel, subControlName);
            if (subControl == null)
                continue;

            // Reuse the PropertyInfo captured in the field metadata (as ExtractValue does); fall back to a
            // by-name lookup on the value's runtime type.
            var property = subField.AttributeProvider as PropertyInfo
                ?? value.GetType().GetProperty(subField.Name);
            if (property == null)
                continue;

            var subValue = property.GetValue(value);

            // Reuse the strategy captured on the sub-control during CreateControl; fall back to the registry.
            var subStrategy = (subControl.Tag as OperationSupport.ControlMetadata)?.Strategy
                ?? ParameterControlRegistry.Instance.GetStrategy(subField);
            subStrategy.SetValue(subControl, subValue, subField);
        }
    }
}
