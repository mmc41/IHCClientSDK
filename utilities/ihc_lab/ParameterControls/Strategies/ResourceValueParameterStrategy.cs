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
/// <para>
/// TODO (deferred): this strategy is registered in the <c>ParameterControlRegistry</c> but is intentionally
/// <b>not reachable</b> from the running Lab: <c>OperationFilterConfiguration.ContainsUnsupportedType</c>
/// excludes operations with <c>ResourceValue</c> parameters from the GUI. Enabling requires: (a) relaxing
/// that filter; (b) the service-to-GUI re-entrancy guard (<c>MainWindow.isSyncingFromService</c>); and
/// (d) typed-payload editing: currently only ResourceID and ValueKind are edited and <c>ExtractValue</c>
/// builds an empty union value.
/// </para>
/// </remarks>
public class ResourceValueParameterStrategy : ParameterControlStrategyBase
{
    /// <summary>
    /// Determines if this strategy can handle ResourceValue types.
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        return field.Type == typeof(ResourceValue);
    }

    /// <summary>
    /// Creates a StackPanel with NumericUpDown for ResourceID and ComboBox for ValueKind.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        EnsureCanHandle(field);

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

        ApplyDescriptionTooltip(stackPanel, field);

        return stackPanel;
    }

    // SubscribeToValueChanged: ResourceValue editing is not yet wired for live GUI->service sync (see the
    // deferred-wiring TODO in the class remarks), so the base no-op is inherited.

    /// <summary>
    /// Extracts ResourceID and ValueKind from controls and creates a ResourceValue instance.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var stackPanel = RequireControl<StackPanel>(control);

        var resourceIdControl = FindResourceIdControl(stackPanel);
        if (resourceIdControl == null)
            throw new InvalidOperationException(
                "Could not find ResourceID NumericUpDown control");

        var valueKindControl = FindValueKindControl(stackPanel);
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
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var stackPanel = RequireControl<StackPanel>(control);

        var resourceIdControl = FindResourceIdControl(stackPanel);
        var valueKindControl = FindValueKindControl(stackPanel);

        if (value is not ResourceValue resourceValue)
        {
            // No ResourceValue to display: reset to defaults (ResourceID 0, first ValueKind).
            if (resourceIdControl != null)
                resourceIdControl.Value = 0;
            if (valueKindControl != null)
                valueKindControl.SelectedIndex = 0;
            return;
        }

        if (resourceIdControl != null)
        {
            resourceIdControl.Value = resourceValue.ResourceID;
        }

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
    /// Finds the ResourceID NumericUpDown among the strategy's child controls.
    /// </summary>
    private static NumericUpDown? FindResourceIdControl(StackPanel stackPanel)
    {
        return stackPanel.Children
            .OfType<NumericUpDown>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ResourceID") == true);
    }

    /// <summary>
    /// Finds the ValueKind ComboBox among the strategy's child controls.
    /// </summary>
    private static ComboBox? FindValueKindControl(StackPanel stackPanel)
    {
        return stackPanel.Children
            .OfType<ComboBox>()
            .FirstOrDefault(c => c.Name?.EndsWith(".ValueKind") == true);
    }
}
