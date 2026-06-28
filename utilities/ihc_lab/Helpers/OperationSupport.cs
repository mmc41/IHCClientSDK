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

        // Clear any existing parameter controls before adding new ones
        parametersPanel.Children.Clear();

        for (int i = 0; i < operationMetadata.Parameters.Length; i++)
        {
            // A CancellationToken parameter is harness-injected (auto-filled from LabAppService's stream token,
            // D11): it is not user-edited, so no control is built for it. Its argument slot keeps its default and
            // is replaced at invoke time. Skipping does not shift the other parameters' index-based control names.
            if (operationMetadata.Parameters[i].Type == typeof(System.Threading.CancellationToken))
                continue;

            AddFieldControls(parametersPanel, operationMetadata.Parameters[i], i.ToString());
        }

        // Show an explicit note for parameter-less operations so the panel does not look empty/broken.
        if (parametersPanel.Children.Count == 0)
        {
            parametersPanel.Children.Add(new TextBlock
            {
                Text = "No parameters required.",
                FontStyle = Avalonia.Media.FontStyle.Italic,
                Foreground = Avalonia.Media.Brushes.Gray
            });
        }
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

        // Get strategy and create control
        var strategy = ParameterControlRegistry.Instance.GetStrategy(field);
        var control = strategy.CreateControl(field, prefix);

        // Store strategy reference in control's Tag for later value extraction
        control.Tag = new ControlMetadata
        {
            Field = field,
            Strategy = strategy
        };

        // Build the "label : control" row via the shared helper so labels line up across nesting levels. The label
        // tooltip shows the description only (no leaked .NET type name) and only when a description exists.
        var rowPanel = LabeledRow(field.Name + ":", control, tooltip: field.Description);
        rowPanel.Margin = new Thickness(0, 0, 0, 15);
        parent.Children.Add(rowPanel);
    }

    /// <summary>
    /// Builds a horizontal "label : editor" row with a fixed-width label column, used for every parameter row so
    /// labels align across nesting levels (top-level, complex sub-fields, ResourceValue payload). The editor stays
    /// a direct child of the returned panel - container strategies locate it one level deep by name - so this must
    /// not introduce extra wrapping.
    /// </summary>
    /// <param name="label">The label text.</param>
    /// <param name="editor">The editor control placed next to the label.</param>
    /// <param name="labelWidth">Fixed label-column width for consistent alignment; pass <see cref="double.NaN"/> to
    /// auto-size the label (used by inline complex-type sub-field rows that should not reserve a fixed column).</param>
    /// <param name="labelAlignment">Vertical alignment of the label (Top suits tall editors, Center inline ones).</param>
    /// <param name="tooltip">Optional tooltip; applied only when non-empty, matching control tooltip behaviour.</param>
    public static StackPanel LabeledRow(
        string label,
        Control editor,
        double labelWidth = 150,
        Avalonia.Layout.VerticalAlignment labelAlignment = Avalonia.Layout.VerticalAlignment.Top,
        string? tooltip = null)
    {
        var row = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10
        };

        var legend = new TextBlock
        {
            Text = label,
            Width = labelWidth,
            Margin = new Thickness(0, 0, 5, 0),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = labelAlignment
        };
        if (!string.IsNullOrWhiteSpace(tooltip))
            ToolTip.SetTip(legend, tooltip);

        row.Children.Add(legend);
        row.Children.Add(editor);
        return row;
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
    /// Finds a control by name, searching the given control and its descendant panels recursively.
    /// </summary>
    /// <param name="parent">The control (typically a panel) to search in.</param>
    /// <param name="name">The name of the control to find.</param>
    /// <returns>The control if found, otherwise null.</returns>
    public static Control? FindControlByName(Control parent, string name)
    {
        if (parent.Name == name)
        {
            return parent;
        }

        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    var found = FindControlByName(childControl, name);
                    if (found != null)
                    {
                        return found;
                    }
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
