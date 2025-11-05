using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Ihc;
using Ihc.App;

namespace IhcLab;

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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OperationSupport) + "." + nameof(AddFieldControls), ActivityKind.Internal);
        activity?.SetParameters(
            (nameof(parent), parent.Name ?? ""),
            (nameof(field), field),
            (nameof(prefix), prefix)
        );
        
        // Add legend (TextBlock label) for this parameter
        var legend = new TextBlock
        {
            Text = field.Name + ":",
            Margin = new Thickness(0, 10, 5, 2),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        };
        ToolTip.SetTip(legend, $"Type: {field.Type.Name}: {field.Description}");

        bool useSingleControl = field.SubTypes.Length == 0 || field.IsFile;
        if (useSingleControl)
        {
            parent.Children.Add(legend);

            // Create a DynField control for this parameter
            var dynField = new DynField
            {
                TypeForControl = field.Type,
                Margin = new Thickness(0, 0, 20, 15),
                Name = prefix,  // prefix is now the index path (e.g., "0", "1.2", "2.1.0")
                Tag = field
            };

            // Add the control to the ParametersPanel
            parent.Children.Add(dynField);
        }
        else
        {   // Complex types like records or arrays.
            // Wrap these controls in a dynamically created StackPanel with horizontal orientation
            var stackPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            parent.Children.Add(stackPanel);

            stackPanel.Children.Add(legend);
            for (int i = 0; i < field.SubTypes.Length; i++)
            {
                string subIndexPath = $"{prefix}.{i}";
                AddFieldControls(stackPanel, field.SubTypes[i], subIndexPath);
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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OperationSupport) + "." + nameof(SetUpParameterControls), ActivityKind.Internal);
        activity?.SetParameters(
            (nameof(parametersPanel), parametersPanel.Name ?? ""),
            (nameof(operationMetadata), operationMetadata)
        );

        ClearControls(parametersPanel);

        for (int i = 0; i < operationMetadata.Parameters.Length; i++)
        {
            AddFieldControls(parametersPanel, operationMetadata.Parameters[i], i.ToString());
        }
    }

    public static void ClearControls(Panel parametersPanel)
    {
        // Clear any existing child controls under panel with Name ParametersPanel
        parametersPanel.Children.Clear();
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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OperationSupport) + "." + nameof(GetParameterValues), ActivityKind.Internal);
        activity?.SetParameters(
            (nameof(parametersPanel), parametersPanel.Name ?? ""),
            (nameof(parameters), parameters)
        );

        var values = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            values[i] = GetFieldValue(parametersPanel, parameter, i.ToString()) ?? throw new InvalidOperationException($"Failed to get value for parameter {parameter.Name}");
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
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(OperationSupport) + "." + nameof(GetFieldValue), ActivityKind.Internal);
        activity?.SetParameters(
            (nameof(parent), parent.Name ?? ""),
            (nameof(field), field),
            (nameof(prefix), prefix)
        );

        // prefix is now the complete index path (e.g., "0", "1.2", "2.1.0")
        string indexPath = prefix;

        // For simple types (primitives and string), find the DynField control and get its value
        if (field.IsSimple)
        {
            var dynField = FindDynFieldByName(parent, indexPath);
            if (dynField != null)
            {
                return dynField.Value ?? LabAppService.OperationItem.GetDefaultValue(field.Type);
            }
            return LabAppService.OperationItem.GetDefaultValue(field.Type);
        }

        if (field.IsFile)
        {
            var dynField = FindDynFieldByName(parent, indexPath);
            if (dynField != null && dynField.Value != null)
            {
                // Create instance of the type by calling constructor with dynField.Value as single argument
                try
                {
                    return Activator.CreateInstance(field.Type, dynField.Value);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create instance of {field.Type.Name} with value '{dynField.Value}'. Copy-constructor missing?", ex);
                }
            }
            return LabAppService.OperationItem.GetDefaultValue(field.Type);
        }

        // For arrays, handle specially
        if (field.IsArray)
        {
            // For now, return empty array of the element type
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
                return LabAppService.OperationItem.GetDefaultValue(field.Type);
            }

            // Set each property from the subtypes
            for (int i = 0; i < field.SubTypes.Length; i++)
            {
                string subIndexPath = $"{indexPath}.{i}";
                var subValue = GetFieldValue(parent, field.SubTypes[i], subIndexPath);
                var property = field.Type.GetProperty(field.SubTypes[i].Name);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, subValue);
                }
            }

            return instance;
        }

        // Default: return default value for the type
        return LabAppService.OperationItem.GetDefaultValue(field.Type);
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
}
