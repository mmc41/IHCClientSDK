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
        /// <remarks><paramref name="oldIndex"/>/<paramref name="newIndex"/> let a caller that already HOLDS either
        /// side's index hand it over: a <see cref="Project"/> holds its own weakly, so a session mid-transition is
        /// the only thing keeping the pre-edit one alive, and passing it is what stops the diff re-walking a tree
        /// that was indexed a moment ago. Omit either and it is taken from the project, building if nothing holds
        /// one. Each index is checked against the project it is handed beside; an index of another tree is
        /// refused, not diffed.</remarks>
        internal static ProjectChangeSet Diff(
            Project old, Project updated, int baseVersion, int newVersion, string origin, string label,
            ProjectIndex? oldIndex = null, ProjectIndex? newIndex = null)
        {
            // The diff walks both sides and is run per commit, so its cost tracks the project size. Its four
            // set counts are also the shape of the edit - an ordinary edit touching hundreds of elements is
            // a fact worth seeing, and nothing else reports it. Through the core, because a fault here has
            // no declared identity of its own: an unexpected throw that the span recorded as a success would
            // be a commit that silently produced no change set.
            return Telemetry.Run(nameof(Diff), scope =>
            {
                oldIndex ??= old.ElementIndex;
                newIndex ??= updated.ElementIndex;
                // The one place in the engine where an index is still PAIRED with a project rather than
                // resolved through it, so the one place the wrong two can be paired. Diffing another tree's
                // maps against this one's elements yields a plausible-looking change set describing a
                // document that never existed, which a reconciler would then apply.
                const string Mispaired = "An index handed to the diff was built from a different tree than "
                    + "the project it was handed beside.";
                oldIndex.RequireBuiltFor(old, Mispaired);
                newIndex.RequireBuiltFor(updated, Mispaired);

                FrozenSet<ElementId> added = newIndex.Ids.Where(id => oldIndex.FindById(id) is null).ToFrozenSet();
                FrozenSet<ElementId> removed = oldIndex.Ids.Where(id => newIndex.FindById(id) is null).ToFrozenSet();

                var changed = new HashSet<ElementId>();
                var childListChanged = new HashSet<ElementId>();
                foreach (ElementId id in oldIndex.Ids)
                {
                    // The old side always resolves — an id is only a key because some element carries it — so this
                    // reads as one condition with the case that does occur: an id the new side no longer has.
                    if (oldIndex.FindById(id) is not { } oldEl || newIndex.FindById(id) is not { } newEl)
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

                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.DiffAddedCount, added.Count);
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.DiffRemovedCount, removed.Count);
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.DiffChangedCount, changed.Count);
                scope.Activity?.SetTag(SdkTelemetryRegistry.Attributes.DiffChildListChangedCount, childListChanged.Count);

                return new ProjectChangeSet(
                    added, removed, changed.ToFrozenSet(), childListChanged.ToFrozenSet(),
                    metadataChanged, FullReload: false, baseVersion, newVersion, origin, label);
            });
        }

        /// <summary>This diff's entry point into the instrumentation core.</summary>
        private static readonly OperationTelemetry Telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(ProjectChangeSet));

        // Equal on the element's own tag+attributes and its id-LESS subtree (recursing into id-less children only,
        // stopping at id-bearing descendants, which carry their own ids). An inequality here is the roll-up: an
        // id-less descendant's change surfaces as its nearest id-bearing ancestor being Changed.
        private static bool SelfAndIdlessEqual(ProjectElement a, ProjectElement b)
        {
            if (a.Tag != b.Tag || a.Attrs != b.Attrs)
            {
                return false;
            }
            List<ProjectElement> aIdless = a.Children.Where(c => c.Id is null).ToList();
            List<ProjectElement> bIdless = b.Children.Where(c => c.Id is null).ToList();
            return aIdless.Count == bIdless.Count
                && aIdless.Zip(bIdless, SelfAndIdlessEqual).All(equal => equal);
        }

        private static List<ElementId> ChildIdSequence(ProjectElement element) =>
            element.Children.Where(c => c.Id is not null).Select(c => c.Id!.Value).ToList();

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
