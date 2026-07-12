#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ihc.Vis.Schema
{
    /// <summary>
    /// The reciprocal-pair wire grammar (spec ch. 06 §6.4, ch. 08): the follow-link halves and the scene rows that
    /// pair with a partner via their <c>link</c> IDREF. Centralised here — a peer of <see cref="TypeCode"/> and
    /// <see cref="ResourceMaterialization"/> — so the delete cascade, the copy-external-half prune and the link/scene
    /// bijection checks all read ONE source of truth, instead of four scattered literal sets a new scene-capable
    /// family could silently leave one of un-updated (an orphaned partner on delete, or a skipped bijection check).
    /// </summary>
    internal static class ReciprocalTags
    {
        /// <summary>The two follow-link half tags (a from-half on the source resource, a to-half on the sink).</summary>
        public static readonly IReadOnlySet<string> FollowLinkHalfTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "link_from_resource", "link_to_resource",
        };

        /// <summary>The scene member row tags — the value-carrying half inside a product's <c>scenes</c> container
        /// (its partner <c>scene_link</c> lives inside the FB's <c>resource_scene</c> pin).</summary>
        public static readonly IReadOnlySet<string> SceneMemberTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "scene_dimmer", "scene_relay", "scene_shutter",
        };

        /// <summary>The scene-row tags that pair reciprocally via <c>link</c> (a scene half ↔ its resource row).</summary>
        public static readonly IReadOnlySet<string> SceneHalfTags =
            SceneMemberTags.Concat(new[] { "scene_link" }).ToHashSet(StringComparer.Ordinal);

        /// <summary>Every tag that participates in a reciprocal link/scene pair — the union of the two sets above;
        /// only elements of these types may be cascaded on a delete.</summary>
        public static readonly IReadOnlySet<string> All =
            FollowLinkHalfTags.Concat(SceneHalfTags).ToHashSet(StringComparer.Ordinal);
    }
}
