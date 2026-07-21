#nullable enable
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
using static Ihc.Vis.Editing.ProjectTreeOps;

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// T016 (M6): the delete/copy reference-integrity cluster extracted from <see cref="ProjectEditor"/> — reciprocal
    /// follow-link/scene half collection, the US-009 row cascade, the strict dangling-reference guard and the
    /// copy-prune. These are the pure computations the public <c>DeleteById</c>/<c>CopySubtree</c> orchestrate;
    /// schema-driven checks take the project's <see cref="ProjectSchemaView"/> as a parameter (no editor state).
    /// <see cref="ProjectEditor"/> imports them via <c>using static</c>, so only the schema-bearing calls gained the
    /// extra argument. Built on <see cref="ProjectTreeOps"/> (RemoveById / CollectIds).
    /// </summary>
    internal static class DeleteCascade
    {
        // The reciprocal-pair tags (follow-link halves + scene rows, spec ch. 06 §6.4, ch. 08) — sourced from the
        // schema layer so this delete cascade, the validator's bijection checks and the copy-prune all read one
        // definition; only elements of these types may be cascaded on a delete.
        internal static readonly IReadOnlySet<string> ReciprocalHalfTags = ReciprocalTags.All;

        internal static void CollectLinkPartners(ProjectElement element, List<ElementId> partners)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (ReciprocalHalfTags.Contains(e.Tag)
                    && ElementId.TryParse(e.GetAttribute("link"), out ElementId partner))
                {
                    partners.Add(partner);
                }
            }
        }

        internal static void CollectExternalReciprocalHalves(ProjectElement element, HashSet<ElementId> insideIds, List<ElementId> external)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                // A null-token link is an unwired row, not an external one — the validator's scene-bijection rule
                // deems that a legitimate authored state, so the prune must not make it vanish on copy.
                if (ReciprocalTags.All.Contains(e.Tag)
                    && e.Id is { } halfId
                    && e.GetAttribute("link") is { } linkToken && linkToken != ElementId.NullToken
                    && ElementId.TryParse(linkToken, out ElementId partner)
                    && !insideIds.Contains(partner))
                {
                    external.Add(halfId);                  // reciprocal partner lies outside the copied subtree
                }
            }
        }

        internal static ProjectElement DropExternalReciprocalHalves(ProjectElement source)
        {
            var insideIds = new HashSet<ElementId>();
            CollectIds(source, insideIds);
            var external = new List<ElementId>();
            CollectExternalReciprocalHalves(source, insideIds, external);
            ProjectElement pruned = source;
            foreach (ElementId halfId in external)
            {
                pruned = RemoveById(pruned, halfId);
            }
            return pruned;
        }

        // The vendor US-009 reference cascade (ENG2-A5, §18 M-B = row-only, any-link-slot): every
        // action/condition/event row whose link1 or link2 points into the deleted set is removed WHOLE — its
        // embedded operand children go with it — while parent groups stay (emptied containers survive). Fixpoint
        // because a removed row's own ids join the set and may be referenced by further rows; anything the capture
        // does not pin (scenes bindings, enum typedefs, case criteria) is left for the strict guard to refuse.
        internal static ProjectElement CascadeReferencingRows(ProjectElement tree, HashSet<ElementId> deletedIds, ProjectSchemaView schemaView)
        {
            bool removedAny = true;
            while (removedAny)
            {
                var rows = new List<ProjectElement>();
                void Walk(ProjectElement element)
                {
                    if (element.Tag is "action" or "condition" or "event" && element.Id is not null
                        && RowReferencesDeleted(element, deletedIds, schemaView))
                    {
                        rows.Add(element);    // the whole row goes; no need to look inside it
                    }
                    else
                    {
                        foreach (ProjectElement child in element.ChildrenOrEmpty())
                        {
                            Walk(child);
                        }
                    }
                }
                Walk(tree);
                foreach (ProjectElement row in rows)   // rows are disjoint — Walk never descends into a matched row
                {
                    CollectIds(row, deletedIds);
                    tree = RemoveById(tree, row.Id!.Value);
                }
                removedAny = rows.Count > 0;
            }
            return tree;
        }

        // A row hits when any of its schema-declared IDREFs (today link1/link2 on all three row tags) points into
        // the deleted set — schema-driven like FindDanglingReferences, so a future row IDREF slot cannot be missed
        // here while the strict guard still sees it.
        private static bool RowReferencesDeleted(ProjectElement row, HashSet<ElementId> deletedIds, ProjectSchemaView schemaView) =>
            schemaView.TryGet(row.Tag) is { } schema && !row.Attrs.IsDefaultOrEmpty
            && row.Attrs.Any(a => IsDeletedIdRef(schema, a.Name, a.Value, deletedIds));

        // The one IDREF-into-the-deleted-set test, shared by the cascade (RowReferencesDeleted) and the strict guard
        // (FindDanglingReferences) so the two can never diverge on which references count as hits.
        private static bool IsDeletedIdRef(ElementSchema schema, string name, string value, HashSet<ElementId> deletedIds) =>
            schema.IsIdRef(name) && ElementId.TryParse(value, out ElementId target) && deletedIds.Contains(target);

        internal static List<string> FindDanglingReferences(ProjectElement tree, HashSet<ElementId> deletedIds, ProjectSchemaView schemaView)
        {
            var hits = new List<string>();
            void Walk(ProjectElement element)
            {
                ElementSchema? schema = schemaView.TryGet(element.Tag);
                if (schema is not null)
                {
                    foreach ((string name, string value) in element.AttrsOrEmpty())
                    {
                        if (IsDeletedIdRef(schema, name, value, deletedIds))
                        {
                            hits.Add($"<{element.Tag}> {(element.Id is { } eid ? eid.ToToken() : "?")} {name}='{value}'");
                        }
                    }
                }
                foreach (ProjectElement child in element.ChildrenOrEmpty())
                {
                    Walk(child);
                }
            }
            Walk(tree);
            return hits;
        }
    }
}
