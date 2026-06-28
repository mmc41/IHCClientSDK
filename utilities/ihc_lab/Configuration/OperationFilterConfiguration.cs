using System;
using System.Threading;
using Ihc;
using IhcLab;
using IhcLab.ParameterControls;

namespace Ihc.App;

/// <summary>
/// Builds the predicate that decides which service operations the IHC Lab GUI exposes.
///
/// The GUI only lists operations it can fully two-way sync: render an editable control for every parameter,
/// extract the value back out, and restore a previously entered value. Operations whose parameters use a
/// feature the GUI cannot yet round-trip are filtered out.
///
/// Both operation kinds are invocable: AsyncFunction via the single Run path, and AsyncEnumerable as a live
/// stream via LabAppService.StartStream/StopStream. A CancellationToken parameter is harness-injected
/// (auto-filled from the stream token) and never rendered, so it never excludes an operation.
///
/// ResourceValue is supported: ResourceValueParameterStrategy renders a ResourceID + ValueKind dropdown + a
/// kind-specific payload editor; it is treated as a leaf (the filter does not recurse into its UnionValue).
///
/// Arrays (<c>T[]</c>) and generic collections (<c>IReadOnlyList&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, ...) ARE
/// supported: ArrayParameterStrategy renders them as a dynamic add/remove list, and their support is validated
/// by recursing into the element type (an array of an unsupported element is still excluded by that recursion).
///
/// Complex records are supported: the control strategies build and restore a record's (simple/enum/file/
/// collection) properties via the registry. The parameter metadata expands one level of properties, so a record
/// whose own property is itself a complex record leaves that sub-field with no sub-types; such a field is
/// un-renderable and is now detected via <see cref="ParameterControlRegistry.TryGetStrategy"/> (returning null)
/// and filtered out (rather than crashing with "No strategy found" at selection time).
/// </summary>
public static class OperationFilterConfiguration
{
    /// <summary>
    /// Creates the default operation filter for GUI display. An operation is allowed when every user-editable
    /// parameter has a working control (see <see cref="ContainsUnsupportedType"/>); a CancellationToken parameter
    /// is skipped (harness-injected). Both AsyncFunction and AsyncEnumerable kinds are invocable.
    /// </summary>
    /// <returns>A predicate function that returns true if the operation is supported, false otherwise.</returns>
    public static Func<ServiceOperationMetadata, bool> CreateDefaultFilter()
    {
        return (ServiceOperationMetadata operation) =>
        {
            // AsyncFunction and AsyncEnumerable are both invocable (the latter is streamed via
            // LabAppService.StartStream/StopStream), so the operation kind no longer excludes anything.
            foreach (var parameter in operation.Parameters)
            {
                // A CancellationToken parameter is harness-injected (auto-filled from LabAppService's stream
                // token, D11) and never rendered, so it never excludes an operation.
                if (parameter.Type == typeof(CancellationToken))
                    continue;

                if (ContainsUnsupportedType(parameter))
                    return false;
            }

            return true;
        };
    }

    /// <summary>
    /// Decides whether a field is un-renderable, derived entirely from the control registry (US-D): a field is
    /// supported iff some strategy can build a control for it, and every sub-field that strategy renders via the
    /// registry is itself supported. There are no hand-coded per-type gates - each strategy owns the knowledge of
    /// what it can render (<see cref="ParameterControlRegistry.TryGetStrategy"/>) and which sub-fields it
    /// decomposes into (<see cref="IParameterControlStrategy.GetRenderedSubFields"/>).
    /// </summary>
    /// <param name="field">The field metadata to check.</param>
    /// <returns>True if the field contains a not-yet-supported type, false otherwise.</returns>
    public static bool ContainsUnsupportedType(FieldMetaData field)
    {
        var strategy = ParameterControlRegistry.Instance.TryGetStrategy(field);

        // No strategy can build a control for this field (e.g. a complex sub-field the one-level metadata left
        // with no sub-types) - exclude the operation rather than let it crash at selection.
        if (strategy == null)
            return true;

        // Leaf strategies (scalars, files, the ResourceValue editor) render the field wholesale and report no
        // rendered sub-fields, so the recursion stops there. Container strategies (complex record, collection)
        // report their constituent fields, each of which must also be renderable.
        foreach (var subField in strategy.GetRenderedSubFields(field))
        {
            if (ContainsUnsupportedType(subField))
                return true;
        }

        return false;
    }
}
