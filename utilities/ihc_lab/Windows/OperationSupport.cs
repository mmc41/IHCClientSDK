using System;
using Avalonia;
using Avalonia.Controls;
using Ihc;

namespace ihc_lab;

/// <summary>
/// Support class for handling operation parameters and field controls.
/// </summary>
public static class OperationSupport
{
    /// <summary>
    /// Adds field controls to the specified panel based on field metadata.
    /// Handles both simple and complex types (records/arrays).
    /// </summary>
    /// <param name="parent">The parent panel to add controls to.</param>
    /// <param name="field">The field metadata describing the control to create.</param>
    /// <param name="prefix">The prefix for the control name (used for nested fields).</param>
    public static void AddFieldControls(Panel parent, FieldMetaData field, string prefix)
    {
        // Add legend (TextBlock label) for this parameter
        var legend = new TextBlock
        {
            Text = field.Name + ":",
            Margin = new Thickness(0, 10, 5, 2),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        };
        ToolTip.SetTip(legend, $"Type: {field.Type.Name}: {field.Description}");

        if (field.SubTypes.Length == 0)
        {
            parent.Children.Add(legend);

            // Create a DynField control for this parameter
            var dynField = new DynField
            {
                TypeForControl = field.Type.Name,
                Margin = new Thickness(0, 0, 20, 15),
                Name = prefix + field.Name,
                Tag = field
            };

            // Add the control to the ParametersPanel
            parent.Children.Add(dynField);
        }
        else
        { // Complex types like records or arrays.
            // Wrap these controls in a dynamically created StackPanel with horizontal orientation
            var stackPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            parent.Children.Add(stackPanel);

            stackPanel.Children.Add(legend);
            foreach (var subField in field.SubTypes)
            {
                AddFieldControls(stackPanel, subField, field.Name + ".");
            }
        }
    }

    /// <summary>
    /// Sets up parameter controls for the specified operation.
    /// Clears existing controls and creates new ones based on operation metadata.
    /// </summary>
    /// <param name="parametersPanel">The panel to add parameter controls to.</param>
    /// <param name="operationMetadata">The operation metadata containing parameter information.</param>
    public static void SetUpParameterControls(Panel parametersPanel, ServiceOperationMetadata operationMetadata)
    {
        // Clear any existing child controls under panel with Name ParametersPanel
        parametersPanel.Children.Clear();

        foreach (FieldMetaData parameter in operationMetadata.Parameters)
        {
            AddFieldControls(parametersPanel, parameter, "");
        }
    }

    /// <summary>
    /// Gets parameter values from the controls in the specified panel.
    /// </summary>
    /// <param name="parametersPanel">The panel containing the parameter controls.</param>
    /// <param name="parameters">The parameter metadata to extract values for.</param>
    /// <returns>An array of parameter values.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a parameter value cannot be retrieved.</exception>
    public static object[] GetParameterValues(Panel parametersPanel, FieldMetaData[] parameters)
    {
        var values = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            values[i] = GetFieldValue(parametersPanel, parameter, string.Empty) ?? throw new InvalidOperationException($"Failed to get value for parameter {parameter.Name}");
        }

        return values;
    }

    /// <summary>
    /// Gets the value for a field from the controls in the specified panel.
    /// Handles simple types, arrays, and complex types recursively.
    /// </summary>
    /// <param name="parent">The parent panel containing the field controls.</param>
    /// <param name="field">The field metadata for the value to retrieve.</param>
    /// <param name="prefix">The prefix for the control name (used for nested fields).</param>
    /// <returns>The field value, or null if not found.</returns>
    public static object? GetFieldValue(Panel parent, FieldMetaData field, string prefix)
    {
        string fullName = prefix + field.Name;

        // For simple types (primitives and string), find the DynField control and get its value
        if (field.IsSimple)
        {
            var dynField = FindDynFieldByName(parent, fullName);
            if (dynField != null)
            {
                return dynField.Value ?? GetDefaultValue(field.Type);
            }
            return GetDefaultValue(field.Type);
        }

        // For arrays, handle specially
        if (field.IsArray)
        {
            // For now, return empty array of the element type
            // TODO: Implement array handling with dynamic UI elements
            var elementType = field.Type.GetElementType();
            if (elementType != null)
            {
                return Array.CreateInstance(elementType, 0);
            }
            return Array.Empty<object>();
        }

        // For complex types (records/classes with subtypes)
        if (field.SubTypes.Length > 0)
        {
            // Create an instance of the type
            var instance = Activator.CreateInstance(field.Type);
            if (instance == null)
            {
                return GetDefaultValue(field.Type);
            }

            // Set each property from the subtypes
            foreach (var subField in field.SubTypes)
            {
                var subValue = GetFieldValue(parent, subField, fullName + ".");
                var property = field.Type.GetProperty(subField.Name);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, subValue);
                }
            }

            return instance;
        }

        // Default: return default value for the type
        return GetDefaultValue(field.Type);
    }

    /// <summary>
    /// Finds a DynField control by name in the specified panel.
    /// Searches recursively through child panels.
    /// </summary>
    /// <param name="parent">The parent panel to search in.</param>
    /// <param name="name">The name of the DynField control to find.</param>
    /// <returns>The DynField control if found, otherwise null.</returns>
    public static DynField? FindDynFieldByName(Panel parent, string name)
    {
        // Recursively search for DynField with the given name
        foreach (var child in parent.Children)
        {
            if (child is DynField dynField && dynField.Name == name)
            {
                return dynField;
            }

            if (child is Panel childPanel)
            {
                var found = FindDynFieldByName(childPanel, name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the default value for the specified type.
    /// </summary>
    /// <param name="type">The type to get the default value for.</param>
    /// <returns>The default value for the type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a default value cannot be created for a value type.</exception>
    public static object? GetDefaultValue(Type type)
    {
        if (type == typeof(string))
            return string.Empty;
        if (type.IsValueType)
            return Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Failed to create instance of {type.Name}");
        return null;
    }
}
