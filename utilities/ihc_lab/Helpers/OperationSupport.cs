using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Ihc;
using Ihc.App;
using IhcLab.ParameterControls;

namespace IhcLab;

/// <summary>
/// Support class for handling operation parameters and field controls.
/// </summary>
public static class OperationSupport
{

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
            object? value = GetFieldValue(parametersPanel, parameter, i.ToString());
            values[i] = value ?? throw new InvalidOperationException($"Failed to get value for parameter {parameter.Name}");
        }

        return values;
    }

    /// <summary>
    /// Adds field controls using the strategy pattern.
    /// Creates a row with label and control for the specified field.
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

        // Create a horizontal container for label + control
        var rowPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 15),
            Spacing = 10
        };

        // Add label with fixed width for alignment
        var legend = new TextBlock
        {
            Text = field.Name + ":",
            Width = 150,  // Fixed width for consistent alignment
            Margin = new Thickness(0, 10, 5, 2),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        ToolTip.SetTip(legend, $"Type: {field.Type.Name}: {field.Description}");
        rowPanel.Children.Add(legend);

        // Get strategy and create control
        var registry = ParameterControlRegistry.Instance;
        var strategy = registry.GetStrategy(field);
        var result = strategy.CreateControl(field, prefix);

        // Store strategy reference in control's Tag for later value extraction
        result.Control.Tag = new ControlMetadata
        {
            Field = field,
            Strategy = strategy
        };

        rowPanel.Children.Add(result.Control);
        parent.Children.Add(rowPanel);
    }

    /// <summary>
    /// Gets the value for a field using the strategy pattern (V2 implementation).
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

        // Find the control by name
        var control = FindControlByName(parent, prefix);
        if (control == null)
        {
            return LabAppService.OperationItem.GetDefaultValue(field.Type);
        }

        // Get strategy from control's Tag
        if (control.Tag is ControlMetadata metadata)
        {
            return metadata.Strategy.ExtractValue(control, metadata.Field);
        }

        // Fallback: get strategy from registry
        var registry = ParameterControlRegistry.Instance;
        var strategy = registry.GetStrategy(field);
        return strategy.ExtractValue(control, field);
    }

    /// <summary>
    /// Finds a control by name in the specified panel.
    /// Searches recursively through child panels.
    /// </summary>
    /// <param name="parent">The parent panel to search in.</param>
    /// <param name="name">The name of the control to find.</param>
    /// <returns>The control if found, otherwise null.</returns>
    private static Control? FindControlByName(Panel parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child is Control control && control.Name == name)
            {
                return control;
            }

            if (child is Panel childPanel)
            {
                var found = FindControlByName(childPanel, name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Metadata associated with controls created by strategies.
    /// Stored in Control.Tag for value extraction.
    /// </summary>
    public class ControlMetadata
    {
        public required FieldMetaData Field { get; init; }
        public required IParameterControlStrategy Strategy { get; init; }
    }
}
