using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Ihc;

namespace IhcLab.ParameterControls.Strategies;

/// <summary>
/// Strategy for collection-shaped parameters: arrays (<c>T[]</c>) and generic collections
/// (<c>IReadOnlyList&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>List&lt;T&gt;</c>, ...). It renders a dynamic list
/// with Add/Remove buttons for managing the elements and, on extract, materialises a typed <c>T[]</c> - which
/// is directly assignable to every collection parameter the IHC API uses (all are <c>IReadOnlyList&lt;T&gt;</c>;
/// see decision D8).
/// </summary>
/// <remarks>
/// Two-way sync (decision D9): the collection control is the single sync unit. Add/Remove/element-edit all
/// re-extract the whole parameter via <see cref="SubscribeToValueChanged"/>, which raises the registered
/// parameter-changed handler with the <b>main panel</b> as the sender so the handler resolves the parameter
/// index from the main panel's Name. Element controls are named <c>{container}.Item{i}</c> positionally - that
/// name is internal; extract/restore walk the item containers in visual order, not by name lookup.
/// </remarks>
public class ArrayParameterStrategy : ParameterControlStrategyBase
{
    // Associates the active parameter-changed handler with a specific array main-panel instance. Element/Add/
    // Remove wiring always routes through NotifyChanged, which looks the handler up here - so wiring done at
    // element-creation time works regardless of whether SubscribeToValueChanged has run yet. Weak keys avoid
    // leaking controls and keep multiple array parameters independent.
    private static readonly ConditionalWeakTable<Control, EventHandler> changeHandlers = new();

    // Monotonic id for element control names so they never collide after a remove-then-add. Names are internal;
    // extract/restore address elements positionally (not by name lookup), so this only keeps names unique. The
    // registry is used only on the Avalonia UI thread, so a plain increment is sufficient.
    private static int elementIdCounter;

    /// <summary>
    /// Determines if this strategy can handle array or generic-collection types (excluding string).
    /// </summary>
    public override bool CanHandle(FieldMetaData field)
    {
        // Require the element-type metadata (SubTypes) too: without it CreateControl cannot build the list and
        // would throw. A collection/array field the one-level metadata expansion left with no element sub-field
        // (e.g. a collection PROPERTY of a complex record) is therefore NOT claimed here, so the operation is
        // filtered rather than crashing at selection (US-A7 - filter, don't crash).
        return (field.Type.IsArray || IsGenericCollection(field.Type)) && field.SubTypes.Length > 0;
    }

    /// <summary>
    /// A collection is rendered by building a control for its element via the registry, so the filter recurses
    /// into the element type (carried as the single sub-field).
    /// </summary>
    public override FieldMetaData[] GetRenderedSubFields(FieldMetaData field) => field.SubTypes;

    /// <summary>
    /// Creates a vertical StackPanel with array item controls and add/remove buttons.
    /// </summary>
    public override Control CreateControl(FieldMetaData field, string controlName)
    {
        // EnsureCanHandle already guarantees SubTypes.Length > 0 (CanHandle requires the element metadata).
        EnsureCanHandle(field);

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
            AddArrayItem(mainPanel, itemsPanel, elementField, controlName);
            UpdateItemCount(label, itemsPanel, field.Name);
            NotifyChanged(mainPanel);
        };

        ApplyDescriptionTooltip(mainPanel, field);

        return mainPanel;
    }

    /// <summary>
    /// Registers the parameter-changed handler for this array control. The actual edit signals (Add, each
    /// Remove, each element's own value change) are wired to <see cref="NotifyChanged"/> at element-creation
    /// time and route back to this handler; restore (<see cref="SetValue"/>) wires after setting values so it
    /// does not echo. The main panel is always passed as the sender (decision D9).
    /// </summary>
    public override void SubscribeToValueChanged(Control control, EventHandler handler)
    {
        var mainPanel = RequireControl<StackPanel>(control);
        changeHandlers.AddOrUpdate(mainPanel, handler);
    }

    /// <summary>
    /// Extracts array values from all item controls.
    /// </summary>
    public override object? ExtractValue(Control control, FieldMetaData field)
    {
        var mainPanel = RequireControl<StackPanel>(control);

        if (field.SubTypes.Length == 0)
            throw new InvalidOperationException(
                $"Collection field '{field.Name}' has no SubTypes. Cannot determine element type.");

        var elementField = field.SubTypes[0];
        var elementType = elementField.Type;

        // Find the items panel
        var itemsPanel = FindItemsPanel(mainPanel);

        if (itemsPanel == null)
            return Array.CreateInstance(elementType, 0);

        // Extract values from each item (visual order)
        var values = new List<object?>();
        var elementStrategy = ParameterControlRegistry.Instance.GetStrategy(elementField);

        foreach (var itemContainer in itemsPanel.Children.OfType<StackPanel>())
        {
            // The element control is the second child of the row (see AddItemControl: [indexLabel, element,
            // removeButton]) - addressed positionally, not by excluding control types.
            if (ElementControlOf(itemContainer) is Control ctrl)
            {
                var value = elementStrategy.ExtractValue(ctrl, elementField);
                values.Add(value);
            }
        }

        // Materialise a typed T[] - directly assignable to the declared IReadOnlyList<T> / T[] parameter (D8).
        var array = Array.CreateInstance(elementType, values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            array.SetValue(values[i], i);
        }

        return array;
    }

    /// <summary>
    /// Sets collection values into the controls by creating an item control for each element (visual order).
    /// Accepts any enumerable (the materialised value is a typed array, but this is robust to List&lt;T&gt; etc.).
    /// </summary>
    public override void SetValue(Control control, object? value, FieldMetaData field)
    {
        var mainPanel = RequireControl<StackPanel>(control);

        if (field.SubTypes.Length == 0)
            throw new InvalidOperationException(
                $"Collection field '{field.Name}' has no SubTypes. Cannot determine element type.");

        var elementField = field.SubTypes[0];

        var itemsPanel = FindItemsPanel(mainPanel);
        var label = HeaderLabel(mainPanel);

        if (itemsPanel == null)
            return;

        if (value is not System.Collections.IEnumerable enumerable || value is string)
        {
            itemsPanel.Children.Clear();
            UpdateItemCount(label, itemsPanel, field.Name);
            return;
        }

        var elementStrategy = ParameterControlRegistry.Instance.GetStrategy(elementField);
        var items = enumerable.Cast<object?>().ToList();
        var existingRows = itemsPanel.Children.OfType<StackPanel>().ToList();

        // In-place update when the element count is unchanged: push each value into the existing element control
        // instead of tearing down and recreating every row. A GUI->service->GUI round-trip restores the just-
        // extracted value on every element edit (arrays compare by reference, so they always look "changed");
        // reusing the controls keeps the element the user is editing alive rather than destroying it mid-keystroke
        // (US-A2 focus preservation). Add/Remove adjust the row count before this runs, so they also land here.
        if (existingRows.Count == items.Count)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ElementControlOf(existingRows[i]) is Control elementControl)
                    elementStrategy.SetValue(elementControl, items[i], elementField);
            }
            UpdateItemCount(label, itemsPanel, field.Name);
            return;
        }

        // Count changed (e.g. a fresh restore into an empty/longer list): rebuild the rows. Wiring happens in
        // AddItemControl AFTER the value is set, so the restore does not echo.
        itemsPanel.Children.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            var elementControl = elementStrategy.CreateControl(elementField, NextItemControlName(mainPanel.Name ?? string.Empty));

            // Set the value BEFORE wiring (in AddItemControl) so the restore does not raise a spurious change.
            elementStrategy.SetValue(elementControl, items[i], elementField);

            AddItemControl(mainPanel, itemsPanel, elementControl, elementStrategy, mainPanel.Name ?? string.Empty, i);
        }

        UpdateItemCount(label, itemsPanel, field.Name);
    }

    /// <summary>
    /// Returns true for a non-array, non-string generic collection type (one implementing IEnumerable&lt;T&gt;).
    /// </summary>
    private static bool IsGenericCollection(Type type)
    {
        if (type.IsArray || type == typeof(string))
            return false;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return true;

        return type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    /// Raises the parameter-changed handler registered for this array (if any), always with the main panel as
    /// the sender so the consumer resolves the parameter index from the main panel's Name (decision D9).
    /// </summary>
    private static void NotifyChanged(StackPanel mainPanel)
    {
        if (changeHandlers.TryGetValue(mainPanel, out var handler))
            handler(mainPanel, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a new (default-valued) array item control to the items panel.
    /// </summary>
    private static void AddArrayItem(StackPanel mainPanel, StackPanel itemsPanel, FieldMetaData elementField, string arrayControlName)
    {
        var elementStrategy = ParameterControlRegistry.Instance.GetStrategy(elementField);

        int index = itemsPanel.Children.Count;
        var elementControl = elementStrategy.CreateControl(elementField, NextItemControlName(arrayControlName));
        AddItemControl(mainPanel, itemsPanel, elementControl, elementStrategy, arrayControlName, index);
    }

    /// <summary>
    /// Returns the element control inside an item row, addressed positionally (the row is built as
    /// [indexLabel, elementControl, removeButton] - see <see cref="AddItemControl"/>).
    /// </summary>
    private static Control? ElementControlOf(StackPanel itemContainer)
        => itemContainer.Children.Count > 1 ? itemContainer.Children[1] as Control : null;

    /// <summary>
    /// Builds a unique, never-reused element control name. Names are internal (lookups are positional); this
    /// only guarantees uniqueness so a remove-then-add cannot produce two controls with the same name.
    /// </summary>
    private static string NextItemControlName(string arrayControlName)
        => $"{arrayControlName}.Item{elementIdCounter++}";

    /// <summary>
    /// Creates a container with the element control and a Remove button, and wires both the element's own
    /// value-changed event and the Remove click to re-extract the whole parameter via <see cref="NotifyChanged"/>.
    /// </summary>
    private static void AddItemControl(StackPanel mainPanel, StackPanel itemsPanel, Control elementControl, IParameterControlStrategy elementStrategy, string arrayControlName, int index)
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

        // Editing this element re-extracts the whole array parameter (two-way sync). Wired here - after the
        // caller has set any initial value - so a service-driven restore does not echo back as a user edit.
        elementStrategy.SubscribeToValueChanged(elementControl, (sender, e) => NotifyChanged(mainPanel));

        // Wire up Remove button
        removeButton.Click += (sender, e) =>
        {
            itemsPanel.Children.Remove(itemContainer);

            // Update indices for remaining items
            UpdateItemIndices(itemsPanel);

            // Update count in header
            UpdateItemCount(HeaderLabel(mainPanel), itemsPanel, HeaderFieldName(mainPanel));

            // Removing an element changes the parameter value.
            NotifyChanged(mainPanel);
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
    /// Finds the items panel (the indented element container) inside an array main panel.
    /// </summary>
    private static StackPanel? FindItemsPanel(StackPanel mainPanel)
        => mainPanel.Children.OfType<StackPanel>().FirstOrDefault(p => p.Name == $"{mainPanel.Name}.Items");

    /// <summary>
    /// Finds the header count label inside an array main panel.
    /// </summary>
    private static TextBlock? HeaderLabel(StackPanel mainPanel)
        => mainPanel.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBlock>().FirstOrDefault();

    /// <summary>
    /// Recovers the field name from the header label text (the part before " (N items)").
    /// </summary>
    private static string HeaderFieldName(StackPanel mainPanel)
    {
        string labelText = HeaderLabel(mainPanel)?.Text ?? "";
        int parenIndex = labelText.IndexOf('(');
        return parenIndex > 0 ? labelText.Substring(0, parenIndex).Trim() : "Array";
    }
}
