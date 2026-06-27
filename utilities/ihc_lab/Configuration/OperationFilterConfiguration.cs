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
///   <item>Non-array generic collections (IReadOnlyList&lt;T&gt;, IEnumerable&lt;T&gt;, IList&lt;T&gt;, ...) -
///         no control strategy renders them yet, so operations taking them are excluded.</item>
/// </list>
///
/// Complex records are supported and intentionally NOT filtered: the control strategies build and restore a
/// record's (simple/enum/file) properties via the registry. The parameter metadata expands one level of
/// properties, so a record whose own property is itself a complex record is not representable - such an
/// operation surfaces a control-creation error at selection time rather than being filtered out here.
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
    /// not yet implemented: arrays, ResourceValue / ResourceValue[], or non-array generic collections. Nested
    /// complex records are NOT unsupported here - their sub-fields are checked recursively, and a record's
    /// own simple/enum/file properties are fully wired.
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

        // Non-array generic collection parameters (IReadOnlyList<int>, IEnumerable<T>, IList<T>, List<T>, ...)
        // are not arrays, expose no SubTypes, and have no dedicated control strategy, so the registry would
        // throw "No strategy found" when the GUI tried to build a control. Exclude them until a collection
        // strategy with full two-way GUI wiring lands (same rationale as the array gate above).
        if (IsUnsupportedCollectionType(field.Type))
            return true;

        // Recursively check subtypes (this is how nested complex records are validated, not rejected).
        foreach (var subType in field.SubTypes)
        {
            if (ContainsUnsupportedType(subType))
                return true;
        }

        return false; // by default nothing should be unsupported
    }

    /// <summary>
    /// Returns true for collection-shaped parameter types the GUI cannot yet render: any non-array type that
    /// implements <see cref="System.Collections.IEnumerable"/> (e.g. IReadOnlyList&lt;T&gt;, IList&lt;T&gt;,
    /// List&lt;T&gt;). Arrays are handled by the dedicated <see cref="FieldMetaData.IsArray"/> gate above, and
    /// <see cref="string"/> - though enumerable - is a fully supported simple type and is never a collection here.
    /// </summary>
    private static bool IsUnsupportedCollectionType(Type type)
        => type != typeof(string)
           && !type.IsArray
           && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
}
