#nullable enable
using System;
using System.IO;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Io
{
    /// <summary>
    /// The project-wide id allocator: a single monotonic counter from which every new element draws a fresh
    /// <c>_0x</c> id of the form <c>(counter &lt;&lt; 8) | typeCode</c> (spec ch. 02). The counter pre-increments,
    /// is never decremented/reset/reused (deletes leave permanent holes), and is seeded as the high-water mark
    /// <c>max(parseHex(last_unique_id), maxCounterPresent, maxReferencedCounter)</c> — never trusting a foreign
    /// <c>last_unique_id</c> blindly (spec ch. 02 §2.5 / ch. 10 pitfall 15), and never re-minting the counter of
    /// a dangling IDREF (which would silently resurrect the dead reference). Loaded ids are preserved verbatim;
    /// only added elements allocate. On save the new <see cref="LastUniqueIdToken"/> is written back to the root.
    /// </summary>
    internal sealed class IdAllocator
    {
        internal const long CounterCeiling = 0xFFFFFF;   // 24-bit counter (spec ch. 02 §2.6)

        private long counter;

        public IdAllocator(long seed)
        {
            counter = seed;
        }

        /// <summary>The current counter high-water mark (the value written back as <c>last_unique_id</c>).</summary>
        public long Counter => counter;

        /// <summary>The current counter rendered as the <c>last_unique_id</c> token (<c>_0x</c> + lowercase hex).</summary>
        public string LastUniqueIdToken => HexToken.Format(counter);

        /// <summary>
        /// Pre-increments the counter and returns a fresh id carrying the given type-code suffix. The ceiling is
        /// checked <em>before</em> mutating, so a failed allocation never leaves an out-of-range counter behind
        /// for a later save to persist as <c>last_unique_id</c>.
        /// </summary>
        public ElementId Allocate(int typeCode)
        {
            if (counter >= CounterCeiling)
            {
                throw new InvalidOperationException(
                    $"The .vis id counter is at its 24-bit ceiling (0x{counter:x}); no further ids can be allocated.");
            }
            counter++;
            return new ElementId((int)counter, typeCode);
        }

        /// <summary>
        /// Builds an allocator seeded from a project's <c>last_unique_id</c>, the highest counter actually
        /// present in the tree, and the highest counter any schema-declared IDREF attribute references —
        /// taking the largest so a too-low (or missing) <c>last_unique_id</c> never yields counter collisions
        /// and a fresh allocation never re-mints a dangling reference's counter.
        /// </summary>
        public static IdAllocator ForProject(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            long fromAttribute = HexToken.ParseValueOrDefault(project.LastUniqueId);
            if (fromAttribute > CounterCeiling)
            {
                throw new InvalidDataException(
                    $"Cannot edit: root last_unique_id '{project.LastUniqueId}' exceeds the 24-bit id counter " +
                    "ceiling (0xffffff) — the project's id space is corrupt.");
            }
            long fromTree = MaxCounterPresent(project.Root);
            long fromReferences = MaxReferencedCounter(project.Root, project.SchemaView);
            return new IdAllocator(Math.Max(fromAttribute, Math.Max(fromTree, fromReferences)));
        }

        /// <summary>The highest id counter physically present in the subtree (0 when none).</summary>
        internal static long MaxCounterPresent(ProjectElement element)
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

        private static long MaxReferencedCounter(ProjectElement element, ProjectSchemaView view)
        {
            long max = 0;
            ElementSchema? schema = view.TryGet(element.Tag);
            if (schema is not null && !element.Attrs.IsDefaultOrEmpty)
            {
                foreach ((string name, string value) in element.Attrs)
                {
                    if (schema.IsIdRef(name) && ElementId.TryParse(value, out ElementId reference)
                        && reference.Counter > max)
                    {
                        max = reference.Counter;
                    }
                }
            }
            if (!element.Children.IsDefaultOrEmpty)
            {
                foreach (ProjectElement child in element.Children)
                {
                    long childMax = MaxReferencedCounter(child, view);
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
