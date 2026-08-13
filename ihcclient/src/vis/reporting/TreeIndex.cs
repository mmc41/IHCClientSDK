#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The tree navigation the report builders share: parent pointers, id resolution and ancestor walks over
    /// an immutable project tree (which has no parent links), built once per report, plus the locality (U5/U12)
    /// and terminal-link (A5/U2) queries every report kind asks the same way. Keyed by reference so distinct
    /// nodes never collide.
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
            foreach (ProjectElement child in element.Children)
            {
                parents[child] = element;
                Walk(child);
            }
        }

        public ProjectElement? Parent(ProjectElement element) =>
            parents.TryGetValue(element, out ProjectElement? parent) ? parent : null;

        public ProjectElement? ById(string? idToken) =>
            idToken is not null && byId.TryGetValue(idToken, out ProjectElement? element) ? element : null;

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

        /// <summary>U12: an element's locality is its nearest ancestor group's name (any nesting depth), or
        /// null when it sits outside every group.</summary>
        public string? LocalityName(ProjectElement element) =>
            NearestAncestorOrSelf(element, "group")?.GetAttribute("name");

        /// <summary>
        /// A5/U2: the far-end elements a dataline terminal's links resolve to, in document order. An output's
        /// links are <c>link_to_resource</c>, an input's <c>link_from_resource</c>; a dangling IDREF resolves
        /// to nothing and is skipped (the vendor's empty <c>id(@link)</c> node set).
        /// </summary>
        public IEnumerable<ProjectElement> LinkTargets(ProjectElement terminal)
        {
            string linkTag = terminal.Tag == "dataline_output" ? ReciprocalTags.FollowLinkToTag : ReciprocalTags.FollowLinkFromTag;
            foreach (ProjectElement linkRow in terminal.Children)
            {
                if (linkRow.Tag == linkTag && ById(linkRow.GetAttribute("link")) is { } target)
                {
                    yield return target;
                }
            }
        }

        /// <summary>U5: every group renders as a top-level locality in document order, nesting flattened.</summary>
        public static IEnumerable<ProjectElement> Localities(Project project) =>
            project.Child("groups") is { } groups
                ? groups.Descendants().Where(e => e.Tag == "group")
                : Enumerable.Empty<ProjectElement>();
    }
}
