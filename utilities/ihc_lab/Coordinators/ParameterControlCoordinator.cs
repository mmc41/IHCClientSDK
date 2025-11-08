using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using IhcLab;
using Microsoft.Extensions.Logging;

namespace Ihc.App;

/// <summary>
/// Coordinator responsible for managing parameter control lifecycle in the GUI.
/// Handles creation, event subscription, and cleanup of parameter controls.
/// </summary>
public class ParameterControlCoordinator
{
    private readonly ILogger<ParameterControlCoordinator> logger;

    public ParameterControlCoordinator(ILogger<ParameterControlCoordinator> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Sets up parameter controls for an operation asynchronously.
    /// Creates parameter control controls, waits for layout completion, and subscribes to events.
    /// </summary>
    /// <param name="parametersPanel">Panel to contain the parameter controls.</param>
    /// <param name="operation">Operation whose parameters to display.</param>
    /// <param name="valueChangedHandler">Handler for parameter control ValueChanged events.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public async System.Threading.Tasks.Task SetupControlsAsync(
        Panel parametersPanel,
        LabAppService.OperationItem operation,
        EventHandler? valueChangedHandler)
    {
        if (parametersPanel == null)
            throw new ArgumentNullException(nameof(parametersPanel));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var operationMetadata = operation.OperationMetadata;

        // Create parameter controls with default values
        OperationSupport.SetUpParameterControls(parametersPanel, operationMetadata);

        // Wait for Avalonia layout pass to complete so controls are fully initialized
        // and attached to the visual tree before we try to restore values or subscribe to events
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        // Subscribe to ValueChanged events for real-time GUI → LabAppService synchronization
        if (valueChangedHandler != null)
        {
            SubscribeToValueChanges(parametersPanel, valueChangedHandler);
        }
    }

    /// <summary>
    /// Clears all controls from the parameters panel.
    /// </summary>
    /// <param name="parametersPanel">Panel to clear.</param>
    /// <exception cref="ArgumentNullException">Thrown when parametersPanel is null.</exception>
    public void ClearControls(Panel parametersPanel)
    {
        if (parametersPanel == null)
            throw new ArgumentNullException(nameof(parametersPanel));

        parametersPanel.Children.Clear();
    }

    /// <summary>
    /// Subscribes to ValueChanged events for all parameter control controls in the panel.
    /// Enables real-time synchronization from GUI to LabAppService.
    /// </summary>
    /// <param name="parent">The panel containing parameter control controls.</param>
    /// <param name="handler">Handler to attach to ValueChanged events.</param>
    /// <exception cref="ArgumentNullException">Thrown when parent or handler is null.</exception>
    public void SubscribeToValueChanges(Panel parent, EventHandler handler)
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        SubscribeRecursive(parent, handler);
    }

    /// <summary>
    /// Recursively subscribes to control-specific events in nested panels.
    /// </summary>
    private void SubscribeRecursive(Panel parent, EventHandler handler)
    {
        foreach (var child in parent.Children)
        {
            if (child is Control control && control.Tag is OperationSupport.ControlMetadata)
            {
                // Subscribe to control-specific events
                // This catches the actual strategy control (TextBox, NumericUpDown, etc.)
                SubscribeToControlEvent(control, handler);

                // Also recurse into it if it's a panel (for complex types)
                // Complex parameters have nested controls that also need event subscriptions
                if (control is Panel controlAsPanel)
                {
                    SubscribeRecursive(controlAsPanel, handler);
                }
            }
            else if (child is Panel childPanel)
            {
                // Recursively subscribe to nested panels
                // This will descend into the row StackPanels to find the actual controls
                SubscribeRecursive(childPanel, handler);
            }
        }
    }

    /// <summary>
    /// Subscribes to the appropriate event for a strategy-created control.
    /// </summary>
    private void SubscribeToControlEvent(Control control, EventHandler handler)
    {
        switch (control)
        {
            case TextBox textBox:
                // Use PropertyChanged to detect Text changes (fires immediately when Text property changes)
                textBox.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == nameof(TextBox.Text))
                        handler(s, EventArgs.Empty);
                };
                break;
            case NumericUpDown numeric:
                numeric.ValueChanged += (s, e) => handler(s, EventArgs.Empty);
                break;
            case ComboBox combo:
                combo.SelectionChanged += (s, e) => handler(s, EventArgs.Empty);
                break;
            case DatePicker datePicker:
                datePicker.SelectedDateChanged += (s, e) => handler(s, EventArgs.Empty);
                break;
            case StackPanel stackPanel when stackPanel.Children.OfType<ToggleButton>().Any():
                // Special case: BoolParameterStrategy creates a StackPanel with RadioButtons
                // Subscribe to each RadioButton's IsCheckedChanged event
                foreach (var radioButton in stackPanel.Children.OfType<ToggleButton>())
                {
                    radioButton.IsCheckedChanged += (s, e) =>
                    {
                        // Pass the StackPanel (which has the metadata) as sender, not the RadioButton
                        handler(stackPanel, EventArgs.Empty);
                    };
                }
                break;
        }
    }

    /// <summary>
    /// Unsubscribes from ValueChanged events for all parameter control controls in the panel.
    /// Should be called during cleanup to prevent memory leaks.
    /// </summary>
    /// <param name="parent">The panel containing parameter control controls.</param>
    /// <param name="handler">Handler to detach from ValueChanged events.</param>
    /// <exception cref="ArgumentNullException">Thrown when parent or handler is null.</exception>
    public void UnsubscribeFromValueChanges(Panel parent, EventHandler handler)
    {
        if (parent == null)
            throw new ArgumentNullException(nameof(parent));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        UnsubscribeRecursive(parent, handler);
    }

    /// <summary>
    /// Recursively unsubscribes from control-specific events in nested panels.
    /// </summary>
    private void UnsubscribeRecursive(Panel parent, EventHandler handler)
    {
        foreach (var child in parent.Children)
        {
            if (child is Control control && control.Tag is OperationSupport.ControlMetadata)
            {
                // Unsubscribe from control-specific events
                // This catches the actual strategy control (TextBox, NumericUpDown, etc.)
                UnsubscribeFromControlEvent(control, handler);
            }
            else if (child is Panel childPanel)
            {
                // Recursively unsubscribe from nested panels
                // This will descend into the row StackPanels to find the actual controls
                UnsubscribeRecursive(childPanel, handler);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from the appropriate event for a strategy-created control.
    /// NOTE: Unsubscription doesn't work perfectly because lambdas were used
    /// during subscription. However, this is acceptable because controls are cleared when
    /// switching operations, which removes all event handlers.
    /// </summary>
    private void UnsubscribeFromControlEvent(Control control, EventHandler handler)
    {
        // Event handlers use lambdas, so we can't unsubscribe the exact same handler.
        // This is acceptable because:
        // 1. Controls are cleared when switching operations (ClearControls removes all controls)
        // 2. The lambdas will be garbage collected when controls are removed
        // 3. No memory leak occurs in practice

        // If precise unsubscription is needed in the future, we would need to store
        // lambda references in a dictionary keyed by control instance.
    }
}
