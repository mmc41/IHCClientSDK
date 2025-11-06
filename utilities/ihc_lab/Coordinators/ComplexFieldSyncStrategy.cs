using System;
using System.Linq;
using Avalonia.Controls;
using IhcLab;

namespace Ihc.App;

/// <summary>
/// Strategy for complex types with sub-properties (recursive mapping).
/// Handles types that have SubTypes defined in metadata.
/// </summary>
public class ComplexFieldSyncStrategy : IFieldSyncStrategy
{
    private readonly IFieldSyncStrategy[] strategies;

    public ComplexFieldSyncStrategy(IFieldSyncStrategy[] strategies)
    {
        this.strategies = strategies;
    }

    public bool CanHandle(FieldMetaData field)
    {
        return field.SubTypes.Length > 0;
    }

    public object? ExtractValueFromGui(Panel parent, FieldMetaData field, string indexPath)
    {
        // For complex types, we don't extract values during GUI → Service sync
        // OperationSupport.GetParameterValues() handles this at a higher level
        // This method is not used for GUI → Service direction
        throw new NotSupportedException("Complex field extraction is handled by OperationSupport.GetParameterValues()");
    }

    public void SetValueInGui(Panel parent, FieldMetaData field, object? value, string indexPath)
    {
        // Skip if no subtypes or value is null
        if (field.SubTypes.Length == 0 || value == null)
            return;

        // Recursively update each sub-field
        for (int i = 0; i < field.SubTypes.Length; i++)
        {
            var subField = field.SubTypes[i];
            string subIndexPath = $"{indexPath}.{i}";

            // Get the property value from the complex object using reflection
            var property = field.Type.GetProperty(subField.Name);
            if (property != null && property.CanRead)
            {
                var subValue = property.GetValue(value);

                // Find the appropriate strategy for this sub-field
                var strategy = strategies.FirstOrDefault(h => h.CanHandle(subField));
                if (strategy != null)
                {
                    strategy.SetValueInGui(parent, subField, subValue, subIndexPath);
                }
            }
        }
    }
}
