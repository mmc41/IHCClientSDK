using System;
using Avalonia.Controls;
using IhcLab;

namespace Ihc.App;

/// <summary>
/// Strategy for simple types and file types (single DynField mapping).
/// Handles direct value mapping without recursion.
/// </summary>
public class SimpleFieldSyncStrategy : IFieldSyncStrategy
{
    public bool CanHandle(FieldMetaData field)
    {
        return field.IsSimple || field.IsFile;
    }

    public object? ExtractValueFromGui(Panel parent, FieldMetaData field, string indexPath)
    {
        var dynField = OperationSupport.FindDynFieldByName(parent, indexPath);
        return dynField?.Value;
    }

    public void SetValueInGui(Panel parent, FieldMetaData field, object? value, string indexPath)
    {
        var dynField = OperationSupport.FindDynFieldByName(parent, indexPath);
        if (dynField != null)
        {
            dynField.Value = value;
        }
    }
}
