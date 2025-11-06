using System;
using Avalonia.Controls;
using Avalonia.Threading;
using IhcLab;
using Microsoft.Extensions.Logging;

namespace Ihc.App;

/// <summary>
/// Coordinator responsible for managing parameter control lifecycle in the GUI.
/// Handles creation, event subscription, and cleanup of DynField controls.
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
    /// Creates DynField controls, waits for layout completion, and subscribes to events.
    /// </summary>
    /// <param name="parametersPanel">Panel to contain the parameter controls.</param>
    /// <param name="operation">Operation whose parameters to display.</param>
    /// <param name="valueChangedHandler">Handler for DynField ValueChanged events.</param>
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

        // Wait for Avalonia layout pass to complete so DynField.OnAttachedToVisualTree() is called
        // and child controls are created before we try to restore values or subscribe to events
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
    /// Subscribes to ValueChanged events for all DynField controls in the panel.
    /// Enables real-time synchronization from GUI to LabAppService.
    /// </summary>
    /// <param name="parent">The panel containing DynField controls.</param>
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
    /// Recursively subscribes to DynField ValueChanged events in nested panels.
    /// </summary>
    private void SubscribeRecursive(Panel parent, EventHandler handler)
    {
        foreach (var child in parent.Children)
        {
            if (child is DynField dynField)
            {
                dynField.ValueChanged += handler;
            }
            else if (child is Panel childPanel)
            {
                // Recursively subscribe to nested panels
                SubscribeRecursive(childPanel, handler);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from ValueChanged events for all DynField controls in the panel.
    /// Should be called during cleanup to prevent memory leaks.
    /// </summary>
    /// <param name="parent">The panel containing DynField controls.</param>
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
    /// Recursively unsubscribes from DynField ValueChanged events in nested panels.
    /// </summary>
    private void UnsubscribeRecursive(Panel parent, EventHandler handler)
    {
        foreach (var child in parent.Children)
        {
            if (child is DynField dynField)
            {
                dynField.ValueChanged -= handler;
            }
            else if (child is Panel childPanel)
            {
                // Recursively unsubscribe from nested panels
                UnsubscribeRecursive(childPanel, handler);
            }
        }
    }
}
