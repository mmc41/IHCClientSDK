using System;
using System.Collections.Immutable;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
namespace Ihc.Vis.Io
{
    /// <summary>
    /// Applies a vendor-like save stamp to a project: rewrites the root <c>id2</c> and the <c>modified</c> element
    /// from the save clock, leaving <c>id1</c> (creation), <c>last_unique_id</c> and every element id untouched.
    /// <c>id2</c> packs day/hour/minute/second (<see cref="PackedStamp"/>) and agrees with <c>modified</c>
    /// (year/month/day/hour/minute) to the minute. Used by the default save path; byte-exact round-trips bypass it.
    /// </summary>
    internal static class MetadataStamper
    {
        public static Project Restamp(Project project, DateTimeOffset localNow)
        {
            ProjectElement root = project.Root;
            if (root.FindChild("modified") is null)
            {
                // Stamping id2 alone would break the documented id2/modified minute-agreement invariant —
                // silently, since nothing re-checks it. Every vendor file and CreateNew output has the child.
                throw new InvalidOperationException(
                    "The project root has no <modified> element, so a vendor-like save cannot re-stamp id2 and " +
                    $"modified together. Use {nameof(ProjectSaveOptions)}.{nameof(ProjectSaveOptions.PreserveExistingMetadata)} " +
                    "to write such a project verbatim.");
            }
            string id2 = PackedStamp.FromDateTime(localNow).ToToken();
            ProjectElement stampedRoot = root.WithAttribute("id2", id2);
            stampedRoot = MapChild(stampedRoot, "modified", m => SetModified(m, localNow));
            return project with { Root = stampedRoot };   // 'with' preserves InlineDtdBlocks (open-world round-trip)
        }

        private static ProjectElement SetModified(ProjectElement modified, DateTimeOffset now)
        {
            ProjectElement result = modified;
            result = result.WithAttribute("year", DecToken.Format(now.Year));
            result = result.WithAttribute("month", DecToken.Format(now.Month));
            result = result.WithAttribute("day", DecToken.Format(now.Day));
            result = result.WithAttribute("hour", DecToken.Format(now.Hour));
            result = result.WithAttribute("minute", DecToken.Format(now.Minute));
            return result;
        }

        /// <summary>Returns a copy of <paramref name="parent"/> with its first child of the given tag transformed.</summary>
        private static ProjectElement MapChild(ProjectElement parent, string tag, Func<ProjectElement, ProjectElement> map)
        {
            ImmutableArray<ProjectElement> children = parent.Children.AsImmutableArray();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Tag == tag)
                {
                    return parent with { Children = children.SetItem(i, map(children[i])) };
                }
            }
            return parent;
        }
    }
}
