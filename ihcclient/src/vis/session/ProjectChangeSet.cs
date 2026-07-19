#nullable enable
using System.Collections.Frozen;
using Ihc.Vis.Model;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The structural delta between two project snapshots (proposal §3.5): which element ids were added, removed,
    /// changed, or had their child list reordered, whether a root metadata block changed, whether the consumer must
    /// fully reload, and the version/origin/label envelope. Carried by <see cref="EditOutcome"/> and
    /// <see cref="ProjectChangedEventArgs"/> so the GUI reconciler can update in place.
    /// </summary>
    /// <remarks>
    /// W2-1 declares the shape so the outcome/event types can reference it; the <b>id-keyed structural diff</b> that
    /// computes it (and the id-less roll-up / metadata / counter-only contract) lands in W2-3.
    /// </remarks>
    public sealed record ProjectChangeSet(
        FrozenSet<ElementId> Added,
        FrozenSet<ElementId> Removed,
        FrozenSet<ElementId> Changed,
        FrozenSet<ElementId> ChildListChanged,
        bool MetadataChanged,
        bool FullReload,
        int BaseVersion,
        int NewVersion,
        string Origin,
        string Label);
}
