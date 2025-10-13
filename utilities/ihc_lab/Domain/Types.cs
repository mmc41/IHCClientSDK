using System;

public static class TypeHelper
{
    public static string MapTypeToControlType(Type type)
    {
        // Handle nullable types by getting the underlying type
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        // Map common .NET types to DynField control types
        if (underlyingType == typeof(string))
            return "string";
        else if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
                 underlyingType == typeof(short) || underlyingType == typeof(byte) ||
                 underlyingType == typeof(uint) || underlyingType == typeof(ulong) ||
                 underlyingType == typeof(ushort) || underlyingType == typeof(sbyte))
            return "integer";
        else if (underlyingType == typeof(bool))
            return "bool";
        else
            // Default to string for unknown types
            return "string";
    }


}