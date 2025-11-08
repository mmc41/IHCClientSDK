using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for handling array parameters (T[]).
/// Creates a dynamic list with add/remove buttons for managing array elements.
/// </summary>
public class ArrayParameterStrategy : IParameterControlStrategy
{
    /// <summary>
    /// Determines if this strategy can handle array types.
    /// </summary>
    public bool CanHandle(FieldMetaData field)
    {
        return field.Type.IsArray;
    }

    /// <summary>
    /// Creates a vertical StackPanel with array item controls and add/remove buttons.
    /// </summary>
    public ControlCreationResult CreateControl(FieldMetaData field, string controlName)
    {
        if (!CanHandle(field))
            throw new NotSupportedException(
                $"ArrayParameterStrategy cannot handle type '{field.Type.FullName}'");

        if (field.SubTypes.Length == 0)
            throw new InvalidOperationException(
                $"Array field '{field.Name}' has no SubTypes. Cannot determine element type.");

        // Get element type metadata
        var elementField = field.SubTypes[0];

        // Create main container (vertical layout)
        var mainPanel = new StackPanel
        {
            Name = controlName,
            Orientation = Orientation.Vertical,
            Spacing = 5
        };

        // Create header with label and Add button
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        var label = new TextBlock
        {
            Text = $"{field.Name} (0 items)",
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var addButton = new Button
        {
            Content = "+ Add",
            Name = $"{controlName}.AddButton"
        };

        headerPanel.Children.Add(label);
        headerPanel.Children.Add(addButton);
        mainPanel.Children.Add(headerPanel);

        // Create items container (will hold array elements)
        var itemsPanel = new StackPanel
        {
            Name = $"{controlName}.Items",
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Margin = new Thickness(20, 5, 0, 0) // Indent items
        };

        mainPanel.Children.Add(itemsPanel);

        // Wire up Add button click handler
        addButton.Click += (sender, e) =>
        {
            AddArrayItem(itemsPanel, elementField, controlName);
            UpdateItemCount(label, itemsPanel, field.Name);
        };

        // Add tooltip if description is available
        if (!string.IsNullOrWhiteSpace(field.Description))
        {
            ToolTip.SetTip(mainPanel, field.Description);
        }

        return new ControlCreationResult
        {
            Control = mainPanel,
            IsComposite = true
        };
    }

    /// <summary>
    /// Extracts array values from all item controls.
    /// </summary>
    public object? ExtractValue(Control control, FieldMetaData field)
    {
        if (control is not StackPanel mainPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        if (field.SubTypes.Length == 0)
            throw new InvalidOperationException(
                $"Array field '{field.Name}' has no SubTypes. Cannot determine element type.");

        var elementField = field.SubTypes[0];
        var elementType = elementField.Type;

        // Find the items panel
        var itemsPanel = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == $"{mainPanel.Name}.Items");

        if (itemsPanel == null)
            return Array.CreateInstance(elementType, 0);

        // Extract values from each item
        var values = new List<object?>();
        var registry = ParameterControlRegistry.Instance;
        var elementStrategy = registry.GetStrategy(elementField);

        foreach (var itemContainer in itemsPanel.Children.OfType<StackPanel>())
        {
            // Find the actual element control (skip the index label and remove button)
            var elementControl = itemContainer.Children
                .FirstOrDefault(c => c is not Button && c is not TextBlock);

            if (elementControl is Control ctrl)
            {
                var value = elementStrategy.ExtractValue(ctrl, elementField);
                values.Add(value);
            }
        }

        // Create typed array
        var array = Array.CreateInstance(elementType, values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            array.SetValue(values[i], i);
        }

        return array;
    }

    /// <summary>
    /// Sets array values into the controls by creating item controls for each element.
    /// </summary>
    public void SetValue(Control control, object? value, FieldMetaData field)
    {
        if (control is not StackPanel mainPanel)
            throw new InvalidOperationException(
                $"Expected StackPanel control but got {control.GetType().Name}");

        if (field.SubTypes.Length == 0)
            throw new InvalidOperationException(
                $"Array field '{field.Name}' has no SubTypes. Cannot determine element type.");

        var elementField = field.SubTypes[0];

        // Find the items panel and label
        var itemsPanel = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == $"{mainPanel.Name}.Items");

        var label = mainPanel.Children
            .OfType<StackPanel>()
            .FirstOrDefault()?
            .Children
            .OfType<TextBlock>()
            .FirstOrDefault();

        if (itemsPanel == null)
            return;

        // Clear existing items
        itemsPanel.Children.Clear();

        if (value == null || value is not Array array)
        {
            UpdateItemCount(label, itemsPanel, field.Name);
            return;
        }

        // Create controls for each array element
        var registry = ParameterControlRegistry.Instance;
        var elementStrategy = registry.GetStrategy(elementField);

        for (int i = 0; i < array.Length; i++)
        {
            string itemControlName = $"{mainPanel.Name}.Item{i}";
            var elementResult = elementStrategy.CreateControl(elementField, itemControlName);

            // Set the value
            elementStrategy.SetValue(elementResult.Control, array.GetValue(i), elementField);

            // Add the control with remove button
            AddItemControl(itemsPanel, elementResult.Control, mainPanel.Name ?? string.Empty, i);
        }

        UpdateItemCount(label, itemsPanel, field.Name);
    }

    /// <summary>
    /// Adds a new array item control to the items panel.
    /// </summary>
    private static void AddArrayItem(StackPanel itemsPanel, FieldMetaData elementField, string arrayControlName)
    {
        var registry = ParameterControlRegistry.Instance;
        var elementStrategy = registry.GetStrategy(elementField);

        int index = itemsPanel.Children.Count;
        string itemControlName = $"{arrayControlName}.Item{index}";

        var elementResult = elementStrategy.CreateControl(elementField, itemControlName);
        AddItemControl(itemsPanel, elementResult.Control, arrayControlName, index);
    }

    /// <summary>
    /// Creates a container with the element control and a Remove button.
    /// </summary>
    private static void AddItemControl(StackPanel itemsPanel, Control elementControl, string arrayControlName, int index)
    {
        var itemContainer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        var itemLabel = new TextBlock
        {
            Text = $"[{index}]",
            VerticalAlignment = VerticalAlignment.Center,
            Width = 40
        };

        var removeButton = new Button
        {
            Content = "Remove",
            VerticalAlignment = VerticalAlignment.Center
        };

        itemContainer.Children.Add(itemLabel);
        itemContainer.Children.Add(elementControl);
        itemContainer.Children.Add(removeButton);

        itemsPanel.Children.Add(itemContainer);

        // Wire up Remove button
        removeButton.Click += (sender, e) =>
        {
            itemsPanel.Children.Remove(itemContainer);

            // Update indices for remaining items
            UpdateItemIndices(itemsPanel);

            // Update count in header
            var mainPanel = FindParentStackPanel(itemsPanel);
            if (mainPanel != null)
            {
                var label = mainPanel.Children
                    .OfType<StackPanel>()
                    .FirstOrDefault()?
                    .Children
                    .OfType<TextBlock>()
                    .FirstOrDefault();

                if (label != null)
                {
                    // Extract field name from label text
                    string labelText = label.Text ?? "";
                    int parenIndex = labelText.IndexOf('(');
                    string fieldName = parenIndex > 0 ? labelText.Substring(0, parenIndex).Trim() : "Array";
                    UpdateItemCount(label, itemsPanel, fieldName);
                }
            }
        };
    }

    /// <summary>
    /// Updates the item count display in the header label.
    /// </summary>
    private static void UpdateItemCount(TextBlock? label, StackPanel itemsPanel, string fieldName)
    {
        if (label != null)
        {
            int count = itemsPanel.Children.Count;
            label.Text = $"{fieldName} ({count} item{(count != 1 ? "s" : "")})";
        }
    }

    /// <summary>
    /// Updates the index labels for all items after removal.
    /// </summary>
    private static void UpdateItemIndices(StackPanel itemsPanel)
    {
        int index = 0;
        foreach (var itemContainer in itemsPanel.Children.OfType<StackPanel>())
        {
            var indexLabel = itemContainer.Children.OfType<TextBlock>().FirstOrDefault();
            if (indexLabel != null)
            {
                indexLabel.Text = $"[{index}]";
            }
            index++;
        }
    }

    /// <summary>
    /// Finds the parent StackPanel (main array control container).
    /// </summary>
    private static StackPanel? FindParentStackPanel(Control control)
    {
        var parent = control.Parent;
        while (parent != null)
        {
            if (parent is StackPanel sp && sp.Name != null && !sp.Name.EndsWith(".Items"))
                return sp;
            parent = parent.Parent;
        }
        return null;
    }
}
