using System;
using Ihc;
using IhcLab;

namespace Ihc.App;

/// <summary>
/// Configuration for filtering which operations are supported in the GUI.
/// Defines rules for excluding operations with unsupported types or characteristics.
/// </summary>
public static class OperationFilterConfiguration
{
    /// <summary>
    /// Creates the default operation filter for GUI display.
    /// Filters out operations with unsupported types (AsyncEnumerable, arrays, ResourceValue).
    /// </summary>
    /// <returns>A predicate function that returns true if the operation is supported, false otherwise.</returns>
    public static Func<ServiceOperationMetadata, bool> CreateDefaultFilter()
    {
        return (ServiceOperationMetadata operation) =>
        {
            // Not sure how to support IAsyncEnumerable so disable it for now
            if (operation.Kind == ServiceOperationKind.AsyncEnumerable)
                return false;

            // Check if any parameter contains unsupported types
            foreach (var parameter in operation.Parameters)
            {
                if (ContainsUnsupportedType(parameter))
                    return false;
            }

            return true; // by default allow everything else
        };
    }

    /// <summary>
    /// Recursively checks if a field contains unsupported / unimplemented types.
    /// Checks for arrays, ResourceValue types, and recursively checks subtypes.
    /// </summary>
    /// <param name="field">The field metadata to check.</param>
    /// <returns>True if the field contains unsupported types, false otherwise.</returns>
    public static bool ContainsUnsupportedType(FieldMetaData field)
    {
        // Check if this field is an array
        if (field.IsArray)
            return true;

        // Check if this field is ResourceValue or ResourceValue[]
        if (field.Type == typeof(ResourceValue) || field.Type == typeof(ResourceValue[]))
            return true;

        // Recursively check subtypes
        foreach (var subType in field.SubTypes)
        {
            if (ContainsUnsupportedType(subType))
                return true;
        }

        return false; // by default nothing should be unsupported
    }
}
