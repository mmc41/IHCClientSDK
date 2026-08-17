#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Editing
{
    /// <summary>
    /// T015 (M6): the pure immutable-tree primitives extracted from <see cref="ProjectEditor"/> — find / replace /
    /// remove a node by id, walk to a parent or build the ancestor path, the element factory and the small
    /// attribute/child helpers. Each takes a <see cref="ProjectElement"/> tree and returns a new one (or a query
    /// result), with NO domain semantics and NO editor state, so the mutable edit session's logic reads over them.
    /// <see cref="ProjectEditor"/> imports these via <c>using static</c>, so its call sites are unchanged.
    /// </summary>
    internal static class ProjectTreeOps
    {
        internal static ProjectElement? FindById(ProjectElement element, ElementId id) =>
            element.FindDescendantOrSelf(e => e.Id == id);

        internal static ProjectElement ReplaceById(ProjectElement element, ElementId id,
            Func<ProjectElement, ProjectElement> map, out bool found)
        {
            if (element.Id == id)
            {
                found = true;
                return map(element);
            }
            if (element.Children.IsEmpty)
            {
                found = false;
                return element;
            }
            bool changed = false;
            found = false;
            var builder = ImmutableArray.CreateBuilder<ProjectElement>(element.Children.Length);
            foreach (ProjectElement child in element.Children)
            {
                if (found)
                {
                    builder.Add(child);   // ids are unique — once the target is replaced, the remaining siblings copy verbatim
                    continue;
                }
                ProjectElement replaced = ReplaceById(child, id, map, out found);
                changed |= !ReferenceEquals(replaced, child);
                builder.Add(replaced);
            }
            return changed ? element with { Children = builder.ToImmutable() } : element;
        }

        internal static ProjectElement RemoveById(ProjectElement element, ElementId id)
        {
            if (element.Children.IsEmpty)
            {
                return element;
            }
            var builder = ImmutableArray.CreateBuilder<ProjectElement>();
            bool changed = false;
            foreach (ProjectElement child in element.Children)
            {
                if (child.Id == id)
                {
                    changed = true;
                    continue;
                }
                ProjectElement kept = RemoveById(child, id);
                changed |= !ReferenceEquals(kept, child);
                builder.Add(kept);
            }
            return changed ? element with { Children = builder.ToImmutable() } : element;
        }

        /// <summary>The batch peer of <see cref="RemoveById"/>: drops every child whose id is in <paramref name="ids"/>
        /// in ONE traversal, rather than re-walking and path-copying the whole tree once per id — a cascade that drops
        /// k elements costs one pass, not k. Same sharing rule: a subtree with nothing removed is returned as-is.
        /// Matched elements are not descended into, exactly as the repeated single-id form behaved.</summary>
        internal static ProjectElement RemoveByIds(ProjectElement element, IReadOnlySet<ElementId> ids)
        {
            if (element.Children.IsEmpty || ids.Count == 0)
            {
                return element;
            }
            var builder = ImmutableArray.CreateBuilder<ProjectElement>();
            bool changed = false;
            foreach (ProjectElement child in element.Children)
            {
                if (child.Id is { } childId && ids.Contains(childId))
                {
                    changed = true;
                    continue;
                }
                ProjectElement kept = RemoveByIds(child, ids);
                changed |= !ReferenceEquals(kept, child);
                builder.Add(kept);
            }
            return changed ? element with { Children = builder.ToImmutable() } : element;
        }

        internal static ProjectElement ReplaceChildByTag(ProjectElement parent, string tag, ProjectElement replacement)
        {
            ImmutableArray<ProjectElement> children = parent.Children.AsImmutableArray();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Tag == tag)
                {
                    return parent with { Children = children.SetItem(i, replacement) };
                }
            }
            return parent;
        }

        internal static ProjectElement ApplyAttributes(ProjectElement element, IReadOnlyList<(string Name, string Value)> attrs)
        {
            ProjectElement result = element;
            foreach ((string name, string value) in attrs)
            {
                result = result.WithAttribute(name, value);
            }
            return result;
        }

        /// <summary>A childless element with the id-led attribute bag — the <c>params</c> shorthand over the shared
        /// <see cref="ProjectElement.Create"/> factory, which owns the id-led-bag convention.</summary>
        internal static ProjectElement SimpleElement(string tag, ElementId id, params (string Name, string Value)[] attrs) =>
            ProjectElement.Create(tag, id, attrs, []);

        internal static EquatableArray<ProjectElement> AppendTo(EquatableArray<ProjectElement> children, ProjectElement child) =>
            children.AsImmutableArray().Add(child);

        internal static ProjectElement? FindParentOf(ProjectElement element, ElementId childId)
        {
            foreach (ProjectElement child in element.Children)
            {
                if (child.Id == childId)
                {
                    return element;
                }
                ProjectElement? found = FindParentOf(child, childId);
                if (found is not null)
                {
                    return found;
                }
            }
            return null;
        }

        internal static bool BuildPath(ProjectElement element, ElementId targetId, List<ProjectElement> chain)
        {
            chain.Add(element);
            if (element.Id == targetId)
            {
                return true;
            }
            foreach (ProjectElement child in element.Children)
            {
                if (BuildPath(child, targetId, chain))
                {
                    return true;
                }
            }
            chain.RemoveAt(chain.Count - 1);
            return false;
        }

        internal static void CollectIds(ProjectElement element, HashSet<ElementId> ids)
        {
            foreach (ProjectElement e in element.DescendantsAndSelf())
            {
                if (e.Id is { } id)
                {
                    ids.Add(id);
                }
            }
        }
    }
}
