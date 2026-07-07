#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Schema;

namespace Ihc.Vis.CatalogCodegen
{
    /// <summary>
    /// The open-world inline-DTD checks shared by <see cref="SelfVerify"/> (products) and <see cref="FbSelfVerify"/>
    /// (function blocks). Both gates must apply the identical capture rule — a component carries an inline-DTD block for
    /// exactly the element types its body uses that the registry does not declare — so it lives here rather than being
    /// copied into each gate where the two could silently drift.
    /// </summary>
    internal static class SelfVerifyShared
    {
        // The source's inline-DTD blocks for element types that are (a) actually instantiated in the body and (b) not
        // declared by the static registry — the open-world grammar that must ride along with the component (and that the
        // insert transform merges into the project). A block DECLARED in the source DTD but never used in the body is a
        // vendor leftover (e.g. a mis-cased 'resource_Light' block above a body that uses registry 'resource_light') and
        // is deliberately excluded — the component neither needs it nor carries it.
        public static ImmutableDictionary<string, string> NonRegistryBlocks(ProjectElement body,
            ImmutableDictionary<string, string> blocks)
        {
            var usedTags = new HashSet<string>(StringComparer.Ordinal);
            CollectTags(body, usedTags);
            return blocks
                .Where(kv => usedTags.Contains(kv.Key) && ProjectSchemaRegistry.TryGet(kv.Key) is null)
                .ToImmutableDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }

        // Compares the built component's inline-DTD blocks against the expected non-registry set (tag → verbatim block).
        // Returns a one-line reason on mismatch, or null when they agree.
        public static string? BlocksMismatch(ImmutableDictionary<string, string> actual,
            ImmutableDictionary<string, string> expected)
        {
            foreach (string tag in expected.Keys)
            {
                if (!actual.TryGetValue(tag, out string? block))
                {
                    return $"inline-DTD block for open-world type '{tag}' not captured";
                }
                if (block != expected[tag])
                {
                    return $"inline-DTD block for '{tag}' differs from source";
                }
            }
            foreach (string tag in actual.Keys)
            {
                if (!expected.ContainsKey(tag))
                {
                    return $"inline-DTD block '{tag}' captured but registry already declares it (dead weight)";
                }
            }
            return null;
        }

        private static void CollectTags(ProjectElement element, HashSet<string> tags)
        {
            tags.Add(element.Tag);
            foreach (ProjectElement child in element.ChildrenOrEmpty())
            {
                CollectTags(child, tags);
            }
        }
    }
}
