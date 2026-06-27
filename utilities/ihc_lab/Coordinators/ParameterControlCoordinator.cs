using System;
using Avalonia.Controls;
using Avalonia.Threading;
using IhcLab;

namespace Ihc.App;

/// <summary>
/// Coordinator responsible for managing parameter control lifecycle in the GUI.
/// Handles creation, event subscription, and cleanup of parameter controls.
/// </summary>
public class ParameterControlCoordinator
{
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
            if (child is Control control && control.Tag is OperationSupport.ControlMetadata metadata)
            {
                // Let the control's own strategy wire its value-changed event(s). This keeps the
                // "which event signals an edit" knowledge inside each strategy rather than in a type
                // switch here, so a new strategy only has to implement SubscribeToValueChanged.
                metadata.Strategy.SubscribeToValueChanged(control, handler);

                // Complex parameters have nested controls (each carrying their own metadata) that also
                // need subscribing, so recurse into panel controls.
                if (control is Panel controlAsPanel)
                {
                    SubscribeRecursive(controlAsPanel, handler);
                }
            }
            else if (child is Panel childPanel)
            {
                // Descend into layout panels (e.g. the row StackPanels) to reach the actual controls.
                SubscribeRecursive(childPanel, handler);
            }
        }
    }
}
