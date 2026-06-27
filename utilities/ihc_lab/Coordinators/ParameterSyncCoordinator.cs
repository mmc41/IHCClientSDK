using System;
using System.Diagnostics;
using Avalonia.Controls;
using IhcLab;
using Microsoft.Extensions.Logging;

namespace Ihc.App;

/// <summary>
/// Coordinator responsible for bidirectional synchronization between GUI controls and LabAppService parameters.
/// The Service-to-GUI direction delegates to each control's own parameter-control strategy (stored in its Tag as
/// OperationSupport.ControlMetadata), so the leaf-vs-complex routing lives in one place - the control strategies -
/// rather than being duplicated here.
/// </summary>
public class ParameterSyncCoordinator
{
    private readonly ILogger<ParameterSyncCoordinator> logger;

    public ParameterSyncCoordinator(ILogger<ParameterSyncCoordinator> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Syncs argument values FROM GUI controls TO LabAppService.SelectedOperation.Arguments.
    /// Extracts values from all parameter controls and updates the operation's method arguments.
    /// </summary>
    /// <param name="parametersPanel">Panel containing the parameter controls.</param>
    /// <param name="operation">Operation item whose arguments will be updated.</param>
    /// <exception cref="ArgumentNullException">Thrown when parametersPanel or operation is null.</exception>
    public void SyncToService(Panel parametersPanel, LabAppService.OperationItem operation)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(ParameterSyncCoordinator) + "." + nameof(SyncToService), ActivityKind.Internal);

        if (parametersPanel == null)
            throw new ArgumentNullException(nameof(parametersPanel));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var operationMetadata = operation.OperationMetadata;

        // Extract parameter values from GUI controls using existing helper
        var parameterValues = OperationSupport.GetParameterValues(parametersPanel, operationMetadata.Parameters);

        operation.SetMethodArgumentsFromArray(parameterValues);

        activity?.SetTag("arguments.synced_count", parameterValues.Length);
    }

    /// <summary>
    /// Syncs argument values FROM LabAppService.SelectedOperation.Arguments TO parameter control GUI controls.
    /// This enables argument persistence - when user returns to an operation, previously set values are restored.
    /// </summary>
    /// <param name="parametersPanel">Panel containing the parameter control controls.</param>
    /// <param name="operation">Operation item whose arguments will be read.</param>
    /// <exception cref="ArgumentNullException">Thrown when parametersPanel or operation is null.</exception>
    public void SyncFromService(Panel parametersPanel, LabAppService.OperationItem operation)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(ParameterSyncCoordinator) + "." + nameof(SyncFromService), ActivityKind.Internal);

        if (parametersPanel == null)
            throw new ArgumentNullException(nameof(parametersPanel));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var operationMetadata = operation.OperationMetadata;
        var savedArguments = operation.GetMethodArgumentsAsArray();

        int restoredCount = 0;
        int failedCount = 0;

        // Restore each top-level parameter by delegating to its control's own strategy.
        for (int i = 0; i < operationMetadata.Parameters.Length; i++)
        {
            var parameter = operationMetadata.Parameters[i];
            var savedValue = savedArguments[i];

            try
            {
                RestoreValue(parametersPanel, savedValue, i.ToString());
                restoredCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                activity?.AddEvent(new ActivityEvent("argument_restore_failed", tags: new ActivityTagsCollection
                {
                    { "parameter.name", parameter.Name },
                    { "parameter.index", i.ToString() },
                    { "parameter.value", savedValue?.ToString() ?? "null" },
                    { "exception.type", ex.GetType().Name },
                    { "exception.message", ex.Message }
                }));
                // Continue with other parameters - don't fail entire restoration
            }
        }

        // restored_count is a top-level parameter count (one per parameter restored without error),
        // not a leaf-field count.
        activity?.SetTag("arguments.restored_count", restoredCount);
        activity?.SetTag("arguments.failed_count", failedCount);
    }

    /// <summary>
    /// Updates GUI controls from a single parameter value change in LabAppService.
    /// Delegates to the target control's own strategy, which handles simple and complex types alike.
    /// </summary>
    /// <param name="parent">Panel containing the parameter control controls.</param>
    /// <param name="field">Field metadata describing the parameter structure.</param>
    /// <param name="value">New value to display in GUI.</param>
    /// <param name="indexPath">Index path for finding the parameter control.</param>
    public void UpdateGuiFromParameter(Panel parent, FieldMetaData field, object? value, string indexPath)
    {
        try
        {
            RestoreValue(parent, value, indexPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update GUI from parameter value");
        }
    }

    /// <summary>
    /// Restores a value into the GUI by locating the control at the given index path and delegating to the
    /// strategy captured in its OperationSupport.ControlMetadata tag. The strategy owns leaf-vs-complex routing
    /// (complex strategies recurse via the registry), so nested complex records restore correctly.
    /// </summary>
    /// <param name="parent">Panel containing the parameter control controls.</param>
    /// <param name="value">Value to set in the GUI.</param>
    /// <param name="indexPath">Index path for finding the parameter control.</param>
    private static void RestoreValue(Panel parent, object? value, string indexPath)
    {
        var control = OperationSupport.FindControlByName(parent, indexPath);
        if (control?.Tag is OperationSupport.ControlMetadata metadata)
        {
            metadata.Strategy.SetValue(control, value, metadata.Field);
        }
    }
}
