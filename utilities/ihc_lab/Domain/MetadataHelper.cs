using Ihc;

namespace IhcLab;

/// <summary>
/// Helper class for working with service operation metadata.
/// </summary>
public static class MetadataHelper
{
    /// <summary>
    /// Check if this app has implemented support for the operation. Used to disable execution button in the app.
    /// NOTE: Logic here represents current state of implementation. May change over time, in which limitations are lifted.
    /// </summary>
    /// <param name="operationMetadata">The operation metadata to check</param>
    /// <returns>True if the operation is supported, false otherwise</returns>
    public static bool IsOperationSupported(ServiceOperationMetadata operationMetadata)
    {
        // Not sure how to support IAsyncEnumerable so disable it for now
        if (operationMetadata.Kind == ServiceOperationKind.AsyncEnumerable)
            return false;

        // Check if any parameter is an array or ResourceValue
        foreach (var parameter in operationMetadata.Parameters)
        {
            if (ContainsUnsupportedType(parameter))
                return false;
        }

        return true; // by default allow everything else.
    }

    /// <summary>
    /// Recursively check if a field contains unsupported types that are currently unsupported by this application.
    /// NOTE: Logic here represents current state of implementation. May change over time, in which limitations are lifted.
    /// </summary>
    /// <param name="field">The field metadata to check</param>
    /// <returns>True if the field contains unsupported types, false otherwise</returns>
    private static bool ContainsUnsupportedType(FieldMetaData field)
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

        return false; // by default nothing should be unsupported.
    }
}
