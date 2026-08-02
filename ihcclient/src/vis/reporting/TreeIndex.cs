#nullable enable
using System;
using System.Collections.Generic;

using Ihc.Vis.Model;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Parent pointers, id resolution and ancestor walks over an immutable project tree (which has no parent
    /// links), built once per report. Keyed by reference so distinct nodes never collide.
    /// </summary>
    internal sealed class TreeIndex
    {
        private readonly Dictionary<ProjectElement, ProjectElement> parents = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, ProjectElement> byId = new(StringComparer.Ordinal);

        public TreeIndex(ProjectElement root) => Walk(root);

        private void Walk(ProjectElement element)
        {
            string? id = element.GetAttribute("id");
            if (id is not null)
            {
                byId.TryAdd(id, element);   // first-wins, matching XPath id() on a well-formed unique-id tree
            }
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                parents[child] = element;
                Walk(child);
            }
        }

        public ProjectElement? Parent(ProjectElement element) =>
            parents.TryGetValue(element, out ProjectElement? parent) ? parent : null;

        public ProjectElement? ById(string? idToken) =>
            idToken is not null && byId.TryGetValue(idToken, out ProjectElement? element) ? element : null;

        public ProjectElement? Ancestor(ProjectElement element, int levels)
        {
            ProjectElement? current = element;
            for (int i = 0; i < levels && current is not null; i++)
            {
                current = Parent(current);
            }
            return current;
        }

        /// <summary>The nearest ancestor (or the element itself) with the given tag, or null when none.</summary>
        public ProjectElement? NearestAncestorOrSelf(ProjectElement element, string tag)
        {
            ProjectElement? current = element;
            while (current is not null && current.Tag != tag)
            {
                current = Parent(current);
            }
            return current;
        }

        // The nearest product_* ancestor (or self) at ANY depth (U8 — a general ancestor walk; the vendor's
        // get_product_* stopped after two levels). Null when none — the "?" case.
        public ProjectElement? NearestProduct(ProjectElement terminal)
        {
            ProjectElement? current = terminal;
            while (current is not null && !current.Tag.StartsWith("product_", StringComparison.Ordinal))
            {
                current = Parent(current);
            }
            return current;
        }
    }
}
