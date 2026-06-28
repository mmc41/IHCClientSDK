#nullable enable
using System;
using System.Globalization;

namespace Ihc.Projects
{
    /// <summary>
    /// The project-wide id allocator: a single monotonic counter from which every new element draws a fresh
    /// <c>_0x</c> id of the form <c>(counter &lt;&lt; 8) | typeCode</c> (spec ch. 02). The counter pre-increments,
    /// is never decremented/reset/reused (deletes leave permanent holes), and is seeded as the high-water mark
    /// <c>max(parseHex(last_unique_id), maxCounterPresent)</c> — never trusting a foreign <c>last_unique_id</c>
    /// blindly (spec ch. 02 §2.5 / ch. 10 pitfall 15). Loaded ids are preserved verbatim; only added elements
    /// allocate. On save the new <see cref="LastUniqueIdToken"/> is written back to the root.
    /// </summary>
    internal sealed class IdAllocator
    {
        private long counter;

        public IdAllocator(long seed)
        {
            counter = seed;
        }

        /// <summary>The current counter high-water mark (the value written back as <c>last_unique_id</c>).</summary>
        public long Counter => counter;

        /// <summary>The current counter rendered as the <c>last_unique_id</c> token (<c>_0x</c> + lowercase hex).</summary>
        public string LastUniqueIdToken => HexToken.Format(counter);

        /// <summary>Pre-increments the counter and returns a fresh id carrying the given type-code suffix.</summary>
        public ElementId Allocate(int typeCode)
        {
            counter++;
            if (counter > 0xFFFFFF)
            {
                throw new InvalidOperationException(
                    "The .vis id counter exceeded its 24-bit ceiling (0xFFFFFF); the project has too many elements.");
            }
            return new ElementId((int)counter, typeCode);
        }

        /// <summary>
        /// Builds an allocator seeded from a project's <c>last_unique_id</c> and the highest counter actually
        /// present in the tree, taking the larger of the two so a too-low (or missing) <c>last_unique_id</c>
        /// never yields counter collisions.
        /// </summary>
        public static IdAllocator ForProject(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            long fromAttribute = ParseHex(project.LastUniqueId);
            long fromTree = MaxCounterPresent(project.Root);
            return new IdAllocator(Math.Max(fromAttribute, fromTree));
        }

        private static long ParseHex(string? lastUniqueId)
        {
            if (lastUniqueId is not null
                && lastUniqueId.StartsWith("_0x", StringComparison.Ordinal)
                && long.TryParse(lastUniqueId.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value)
                && value >= 0)
            {
                return value;
            }
            return 0;
        }

        private static long MaxCounterPresent(ProjectElement element)
        {
            long max = element.Id is { } id ? id.Counter : 0;
            if (!element.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in element.Children)
                {
                    long childMax = MaxCounterPresent(child);
                    if (childMax > max)
                    {
                        max = childMax;
                    }
                }
            }
            return max;
        }
    }
}
