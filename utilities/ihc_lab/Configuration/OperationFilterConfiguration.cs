using System;
using Ihc;
using IhcLab;

namespace Ihc.App;

/// <summary>
/// Builds the predicate that decides which service operations the IHC Lab GUI exposes.
///
/// The GUI only lists operations it can fully two-way sync: render an editable control for every parameter,
/// extract the value back out, and restore a previously entered value. Operations whose parameters use a
/// feature the GUI cannot yet round-trip are filtered out.
///
/// The exclusions below are not-yet-implemented gates, not arbitrary policy. When a feature gains full
/// two-way GUI wiring, its exclusion here should be removed. Currently excluded:
/// <list type="bullet">
///   <item>IAsyncEnumerable operations - streaming results are not supported in the GUI.</item>
///   <item>Arrays - ArrayParameterStrategy exists and is unit-tested, but its two-way GUI wiring is deferred
///         (see the TODO header on ArrayParameterStrategy).</item>
///   <item>ResourceValue / ResourceValue[] - ResourceValueParameterStrategy likewise exists, but its GUI
///         wiring is deferred.</item>
/// </list>
///
/// Nested complex records are fully supported and intentionally NOT filtered: the control strategies build
/// and restore them recursively via the registry.
/// </summary>
public static class OperationFilterConfiguration
{
    /// <summary>
    /// Creates the default operation filter for GUI display. An operation is allowed only when the GUI can
    /// two-way sync all of it: AsyncEnumerable operations are gated out here, and any operation with a
    /// parameter containing a not-yet-supported type (see <see cref="ContainsUnsupportedType"/>) is excluded.
    /// </summary>
    /// <returns>A predicate function that returns true if the operation is supported, false otherwise.</returns>
    public static Func<ServiceOperationMetadata, bool> CreateDefaultFilter()
    {
        return (ServiceOperationMetadata operation) =>
        {
            // IAsyncEnumerable streams results; the GUI has no two-way control for a stream yet, so exclude it.
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
    /// Recursively checks whether a field (or any of its sub-fields) uses a type whose two-way GUI wiring is
    /// not yet implemented: arrays or ResourceValue / ResourceValue[]. Nested complex records are NOT
    /// unsupported - their sub-fields are checked recursively, but the records themselves are fully wired.
    /// </summary>
    /// <param name="field">The field metadata to check.</param>
    /// <returns>True if the field contains a not-yet-supported type, false otherwise.</returns>
    public static bool ContainsUnsupportedType(FieldMetaData field)
    {
        // Deliberate, not an oversight: array and ResourceValue operations are excluded from the GUI even
        // though ArrayParameterStrategy and ResourceValueParameterStrategy exist and are unit-tested. Their
        // two-way GUI wiring is deferred (see the TODO blocks on those strategies); remove the relevant gate
        // below once that wiring lands.

        // Check if this field is an array
        if (field.IsArray)
            return true;

        // Check if this field is ResourceValue or ResourceValue[]
        if (field.Type == typeof(ResourceValue) || field.Type == typeof(ResourceValue[]))
            return true;

        // Recursively check subtypes (this is how nested complex records are validated, not rejected).
        foreach (var subType in field.SubTypes)
        {
            if (ContainsUnsupportedType(subType))
                return true;
        }

        return false; // by default nothing should be unsupported
    }
}
