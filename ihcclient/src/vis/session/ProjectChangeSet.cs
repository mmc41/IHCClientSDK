#nullable enable
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The structural delta between two project snapshots (proposal §3.5): which element ids were added, removed,
    /// changed, or had their child list reordered, whether a root metadata block changed, whether the consumer must
    /// fully reload, and the version/origin/label envelope. Carried by <see cref="EditOutcome"/> and
    /// <see cref="ProjectChangedEventArgs"/> so the GUI reconciler can update in place.
    /// </summary>
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
        string Label)
    {
        /// <summary>
        /// The full-document id-keyed structural diff of <paramref name="old"/> vs <paramref name="updated"/>
        /// (proposal §3.5), honouring the id-less contract: a changed element without an id rolls up into its
        /// nearest id-bearing ancestor's <see cref="Changed"/> entry; the root metadata blocks
        /// (<c>project_info</c>/<c>customer_info</c>/<c>installer_info</c>) report as <see cref="MetadataChanged"/>;
        /// and a root whose only diff is the <c>last_unique_id</c> counter reports nothing (its allocations already
        /// show as <see cref="Added"/>). Correct on id+content comparison alone; sharing is not relied upon.
        /// </summary>
        internal static ProjectChangeSet Diff(
            Project old, Project updated, int baseVersion, int newVersion, string origin, string label)
        {
            Dictionary<ElementId, ProjectElement> oldById = BuildIdMap(old.Root);
            Dictionary<ElementId, ProjectElement> newById = BuildIdMap(updated.Root);

            FrozenSet<ElementId> added = newById.Keys.Where(id => !oldById.ContainsKey(id)).ToFrozenSet();
            FrozenSet<ElementId> removed = oldById.Keys.Where(id => !newById.ContainsKey(id)).ToFrozenSet();

            var changed = new HashSet<ElementId>();
            var childListChanged = new HashSet<ElementId>();
            foreach ((ElementId id, ProjectElement oldEl) in oldById)
            {
                if (!newById.TryGetValue(id, out ProjectElement? newEl))
                {
                    continue;
                }
                if (ReferenceEquals(oldEl, newEl))
                {
                    // The commit path shares untouched subtrees verbatim (Canonicalizer's P3 sharing rule), so the
                    // same instance on both sides settles both questions below at once: same attrs, same id-less
                    // subtree, same child-id sequence. Skipping here is what makes an edit cost its own path rather
                    // than a full-tree comparison — without it every element is deep-compared on every commit.
                    continue;
                }
                if (!SelfAndIdlessEqual(oldEl, newEl))
                {
                    changed.Add(id);   // own attrs or an id-less descendant differs (roll-up)
                }
                if (!ChildIdSequence(oldEl).SequenceEqual(ChildIdSequence(newEl)))
                {
                    childListChanged.Add(id);   // id-bearing children added/removed/reordered at this level
                }
            }

            bool metadataChanged =
                MetadataBlockChanged(old, updated, "project_info")
                || MetadataBlockChanged(old, updated, "customer_info")
                || MetadataBlockChanged(old, updated, "installer_info");

            return new ProjectChangeSet(
                added, removed, changed.ToFrozenSet(), childListChanged.ToFrozenSet(),
                metadataChanged, FullReload: false, baseVersion, newVersion, origin, label);
        }

        private static Dictionary<ElementId, ProjectElement> BuildIdMap(ProjectElement root)
        {
            var map = new Dictionary<ElementId, ProjectElement>();
            Walk(root, map);
            return map;
        }

        private static void Walk(ProjectElement element, Dictionary<ElementId, ProjectElement> map)
        {
            if (element.Id is { } id)
            {
                map.TryAdd(id, element);
            }
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                Walk(child, map);
            }
        }

        // Equal on the element's own tag+attributes and its id-LESS subtree (recursing into id-less children only,
        // stopping at id-bearing descendants, which carry their own ids). An inequality here is the roll-up: an
        // id-less descendant's change surfaces as its nearest id-bearing ancestor being Changed.
        private static bool SelfAndIdlessEqual(ProjectElement a, ProjectElement b)
        {
            if (a.Tag != b.Tag || !ImmutableArrayValue.Equal(a.Attrs, b.Attrs))
            {
                return false;
            }
            List<ProjectElement> aIdless = a.ChildrenOrEmpty().Where(c => c.Id is null).ToList();
            List<ProjectElement> bIdless = b.ChildrenOrEmpty().Where(c => c.Id is null).ToList();
            return aIdless.Count == bIdless.Count
                && aIdless.Zip(bIdless, SelfAndIdlessEqual).All(equal => equal);
        }

        private static List<ElementId> ChildIdSequence(ProjectElement element) =>
            element.ChildrenOrEmpty().Where(c => c.Id is not null).Select(c => c.Id!.Value).ToList();

        private static bool MetadataBlockChanged(Project old, Project updated, string tag)
        {
            ProjectElement? a = old.Child(tag);
            ProjectElement? b = updated.Child(tag);
            if (a is null || b is null)
            {
                return a is not null || b is not null;   // one present, one absent → changed; both absent → not
            }
            return !SelfAndIdlessEqual(a, b);
        }
    }
}
