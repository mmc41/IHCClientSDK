using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using IhcLab;
using Microsoft.Extensions.Logging;

namespace Ihc.App;

/// <summary>
/// Coordinator responsible for bidirectional synchronization between DynField GUI controls and LabAppService parameters.
/// Uses strategy pattern to handle different field types (simple vs complex) consistently.
/// </summary>
public class ParameterSyncCoordinator
{
    private readonly ILogger<ParameterSyncCoordinator> logger;
    private readonly IFieldSyncStrategy[] syncStrategies;

    public ParameterSyncCoordinator(ILogger<ParameterSyncCoordinator> logger)
    {
        this.logger = logger;

        // Initialize strategies (order matters - check simple types before complex)
        var simpleStrategy = new SimpleFieldSyncStrategy();
        this.syncStrategies = new IFieldSyncStrategy[]
        {
            simpleStrategy,
            new ComplexFieldSyncStrategy(new[] { simpleStrategy }) // Complex strategy needs simple strategy for recursion
        };
    }

    /// <summary>
    /// Syncs argument values FROM DynField GUI controls TO LabAppService.SelectedOperation.Arguments.
    /// Extracts values from all parameter controls and updates the operation's method arguments.
    /// </summary>
    /// <param name="parametersPanel">Panel containing the DynField controls.</param>
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
    /// Syncs argument values FROM LabAppService.SelectedOperation.Arguments TO DynField GUI controls.
    /// This enables argument persistence - when user returns to an operation, previously set values are restored.
    /// </summary>
    /// <param name="parametersPanel">Panel containing the DynField controls.</param>
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

        // For each parameter, restore its value using the strategy pattern
        for (int i = 0; i < operationMetadata.Parameters.Length; i++)
        {
            var parameter = operationMetadata.Parameters[i];
            var savedValue = savedArguments[i];

            try
            {
                SetFieldValue(parametersPanel, parameter, savedValue, i.ToString(), ref restoredCount);
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

        activity?.SetTag("arguments.restored_count", restoredCount);
        activity?.SetTag("arguments.failed_count", failedCount);
    }

    /// <summary>
    /// Updates GUI controls from a parameter value change in LabAppService.
    /// Handles both simple types (direct update) and complex types (recursive update) using strategy pattern.
    /// </summary>
    /// <param name="parent">Panel containing the DynField controls.</param>
    /// <param name="field">Field metadata describing the parameter structure.</param>
    /// <param name="value">New value to display in GUI.</param>
    /// <param name="indexPath">Index path for finding the DynField.</param>
    public void UpdateGuiFromParameter(Panel parent, FieldMetaData field, object? value, string indexPath)
    {
        try
        {
            int dummyCount = 0;
            SetFieldValue(parent, field, value, indexPath, ref dummyCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update GUI from parameter value");
        }
    }

    /// <summary>
    /// Unified method to set field values in GUI using strategy pattern.
    /// Handles both simple and complex types by delegating to appropriate strategy.
    /// </summary>
    /// <param name="parent">Panel containing the DynField controls.</param>
    /// <param name="field">Field metadata describing the parameter structure.</param>
    /// <param name="value">Value to set in GUI.</param>
    /// <param name="indexPath">Index path for finding the DynField.</param>
    /// <param name="restoredCount">Running count of successfully restored fields (for telemetry).</param>
    private void SetFieldValue(Panel parent, FieldMetaData field, object? value, string indexPath, ref int restoredCount)
    {
        // Find the appropriate strategy for this field type
        var strategy = syncStrategies.FirstOrDefault(s => s.CanHandle(field));
        if (strategy != null)
        {
            strategy.SetValueInGui(parent, field, value, indexPath);

            // Increment count for simple types only (complex types increment recursively)
            if (strategy is SimpleFieldSyncStrategy)
            {
                restoredCount++;
            }
            else if (strategy is ComplexFieldSyncStrategy && field.SubTypes.Length > 0 && value != null)
            {
                // For complex types, count the number of leaf fields that will be set
                restoredCount += CountLeafFields(field);
            }
        }
    }

    /// <summary>
    /// Counts the number of leaf (simple/file) fields in a complex field hierarchy.
    /// Used for telemetry to track how many individual fields were restored.
    /// </summary>
    private int CountLeafFields(FieldMetaData field)
    {
        int count = 0;
        foreach (var subField in field.SubTypes)
        {
            if (subField.IsSimple || subField.IsFile)
            {
                count++;
            }
            else if (subField.SubTypes.Length > 0)
            {
                count += CountLeafFields(subField);
            }
        }
        return count;
    }
}
