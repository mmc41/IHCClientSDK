#nullable enable
using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Ihc.Projects
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
            string id2 = PackedStamp.FromDateTime(localNow).ToToken();
            ProjectElement stampedRoot = SetAttribute(root, "id2", id2);
            stampedRoot = MapChild(stampedRoot, "modified", m => SetModified(m, localNow));
            return project with { Root = stampedRoot };   // 'with' preserves InlineDtdBlocks (open-world round-trip)
        }

        private static ProjectElement SetModified(ProjectElement modified, DateTimeOffset now)
        {
            ProjectElement result = modified;
            result = SetAttribute(result, "year", Dec(now.Year));
            result = SetAttribute(result, "month", Dec(now.Month));
            result = SetAttribute(result, "day", Dec(now.Day));
            result = SetAttribute(result, "hour", Dec(now.Hour));
            result = SetAttribute(result, "minute", Dec(now.Minute));
            return result;
        }

        private static string Dec(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>Returns a copy of <paramref name="element"/> with the named attribute set (replaced or appended).</summary>
        private static ProjectElement SetAttribute(ProjectElement element, string name, string value)
        {
            ImmutableArray<(string Name, string Value)> attrs =
                element.Attrs.IsDefaultOrEmpty ? ImmutableArray<(string, string)>.Empty : element.Attrs;
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i].Name == name)
                {
                    return element with { Attrs = attrs.SetItem(i, (name, value)) };
                }
            }
            return element with { Attrs = attrs.Add((name, value)) };
        }

        /// <summary>Returns a copy of <paramref name="parent"/> with its first child of the given tag transformed.</summary>
        private static ProjectElement MapChild(ProjectElement parent, string tag, Func<ProjectElement, ProjectElement> map)
        {
            if (parent.Children.IsDefaultOrEmpty)
            {
                return parent;
            }
            ImmutableArray<ProjectElement> children = parent.Children;
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
