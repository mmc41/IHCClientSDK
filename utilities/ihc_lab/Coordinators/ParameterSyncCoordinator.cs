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
    /// Initializes only the service arguments that are still uninitialized (null), using the current default
    /// values of the freshly created GUI controls. Arguments that already hold a value - whether a generated
    /// default or a value entered on a previous visit to this operation - are deliberately left untouched.
    /// </summary>
    /// <remarks>
    /// This exists because complex reference-type parameters default to <c>null</c> on the service side (see
    /// <see cref="LabAppService.OperationItem.GetDefaultValue"/>), yet their freshly built GUI control holds a
    /// valid default instance. Initializing those avoids passing null to the operation. Crucially, unlike a
    /// blanket GUI-&gt;service sync, this never overwrites previously entered values with control defaults, so
    /// argument persistence (switch away and back) keeps working - the saved values are restored by
    /// <see cref="SyncFromService"/> immediately afterwards.
    /// </remarks>
    /// <param name="parametersPanel">Panel containing the parameter controls.</param>
    /// <param name="operation">Operation item whose arguments will be initialized where still missing.</param>
    /// <exception cref="ArgumentNullException">Thrown when parametersPanel or operation is null.</exception>
    public void InitializeUninitializedArguments(Panel parametersPanel, LabAppService.OperationItem operation)
    {
        using var activity = IhcLab.Telemetry.ActivitySource.StartActivity(nameof(ParameterSyncCoordinator) + "." + nameof(InitializeUninitializedArguments), ActivityKind.Internal);

        if (parametersPanel == null)
            throw new ArgumentNullException(nameof(parametersPanel));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var parameters = operation.OperationMetadata.Parameters;
        var currentArguments = operation.GetMethodArgumentsAsArray();

        int initializedCount = 0;

        for (int i = 0; i < parameters.Length; i++)
        {
            // Only fill genuinely uninitialized arguments; never clobber an existing (default or restored) value.
            if (currentArguments[i] != null)
                continue;

            try
            {
                object? value = OperationSupport.GetFieldValue(parametersPanel, parameters[i], i.ToString());
                if (value != null)
                {
                    operation.SetMethodArgument(i, value);
                    initializedCount++;
                }
            }
            catch (Exception ex)
            {
                // Best-effort: a control whose value cannot yet be extracted (e.g. an empty nullable field)
                // simply stays null. Continue with the remaining parameters.
                logger.LogDebug(ex, "Skipped initializing default value for parameter {ParameterName} of operation {OperationName}", parameters[i].Name, operation.OperationMetadata.Name);
            }
        }

        activity?.SetTag("arguments.initialized_count", initializedCount);
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
    /// <param name="value">New value to display in GUI.</param>
    /// <param name="indexPath">Index path for finding the parameter control.</param>
    public void UpdateGuiFromParameter(Panel parent, object? value, string indexPath)
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
